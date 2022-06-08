using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading;
using CloudCqs;
using CloudCqs.Query;
using Microsoft.EntityFrameworkCore;

namespace EfRest.Internal;

internal class GetOneQuery<TEntity, TKey> : Query<(TKey id, string? embed), TEntity>
        where TEntity : class
{
    public GetOneQuery(CloudCqsOptions option, DbContext db, JsonSerializerOptions jsonSerializerOptions, CancellationToken cancellationToken) : base(option)
    {
        var handler = new Handler()
            .Then("Create base query", p =>
            {
                var (id, embed) = p;
                var query = db
                    .Set<TEntity>()
                    .AsNoTracking();
                return (query, id, embed);
            })
            .Then("(embed) Parse json object", p =>
            {
                var (query, id, embedParam) = p;
                var json = embedParam;
                if (json == null) return (query, id, embed: Array.Empty<string>());
                try
                {
                    var embed = JsonSerializer.Deserialize<string[]>(json, jsonSerializerOptions);
                    if (embed == null) throw new BadRequestException(new()
                    {
                        ["embed"] = new[] { $"Invalid json array: {json}" }
                    });

                    return (query, id, embed);
                }
                catch (JsonException e)
                {
                    throw new BadRequestException(new()
                    {
                        ["embed"] = new[] { e.Message }
                    });
                }
            })
            .Then("(embed) Convert json property names to EF's", p =>
            {
                var (query, id, embed) = p;
                var convertedNames = embed
                    .Select(embedItem =>
                    {
                        var names = embedItem
                            .Split('.')
                            .Aggregate(
                            (nameList: Array.Empty<string>(), type: typeof(TEntity)),
                            (accumulator, current) =>
                            {
                                var (nameList, type) = accumulator;
                                var propertyInfo = type
                                    .GetPropertyInfoByJsonName(
                                        current,
                                        jsonSerializerOptions);
                                if (propertyInfo == null)
                                {
                                    throw new BadRequestException(new()
                                    {
                                        ["embed"] = new[] { $"Invalid field name: {embedItem}" }
                                    });
                                }
                                var newNameList = nameList.Append(propertyInfo.Name).ToArray();
                                var propType = propertyInfo.PropertyType;
                                if (propType.IsGenericType
                                    && propType
                                        .GetInterfaces()
                                        .Any(i => i.IsGenericType
                                            && i.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
                                {
                                    var newType = propType.GetGenericArguments().First();
                                    return (newNameList, newType);
                                }
                                return (newNameList, propType);
                            })
                            .nameList;
                        var includeName = string.Join('.', names);
                        return includeName;
                    })
                    .ToArray();
                return (query, id, embed: convertedNames);
            })
            .Then("(embed) Apply embed", p =>
            {
                var (query, id, embed) = p;
                var embedQuery = embed
                    .Aggregate(query, (acc, cur) => acc.Include(cur));
                return (query: embedQuery, id);
            })
            .Then("Get key's PropertyInfo", p =>
            {
                var (query, id) = p;
                var dbSet = db.Set<TEntity>();
                var propertyInfo = dbSet
                    .EntityType
                    .FindPrimaryKey()?
                    .Properties
                    .SingleOrDefault()?
                    .PropertyInfo;
                if (propertyInfo == null)
                {
                    throw new BadRequestException(new()
                    {
                        ["resource"] = new[] { $"Entity must have single primary key." }
                    });
                }
                return (query, id, propertyInfo);
            })
            .Then("Create where expression", p =>
            {
                var (query, id, propertyInfo) = p;
                var entityParameter = Expression.Parameter(typeof(TEntity), "entity");
                var memberAccess = Expression.MakeMemberAccess(entityParameter, propertyInfo);
                var valueExpression = Expression.Convert(Expression.Constant(id), propertyInfo.PropertyType);
                var body = Expression.Equal(memberAccess, valueExpression);
                var predicate = Expression.Lambda<Func<TEntity, bool>>(body, entityParameter);

                return (id, query: query.Where(predicate));
            })
            .Then("Run query", async p =>
            {
                var (id, query) = p;
                var data = await query.SingleOrDefaultAsync(cancellationToken);
                if (data == null)
                {
                    throw new NotFoundException(new()
                    {
                        ["id"] = new[] { $"Id not found: {id}" }
                    });
                }
                return data;
            })
            .Build();

        SetHandler(handler);
    }
}
