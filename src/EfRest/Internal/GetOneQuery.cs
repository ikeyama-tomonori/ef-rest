using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using CloudCqs;
using CloudCqs.Query;
using Microsoft.EntityFrameworkCore;

namespace EfRest.Internal
{
    internal class GetOneQuery<TEntity> : Query<(string id, NameValueCollection param, CancellationToken cancellationToken), HttpResponseMessage>
        where TEntity : class
    {
        public GetOneQuery(CloudCqsOptions option, DbContext db, JsonSerializerOptions jsonSerializerOptions) : base(option)
        {
            var handler = new Handler()
                .Then("Create base query", props =>
                {
                    var (id, param, cancellationToken) = props;
                    var query = db
                        .Set<TEntity>()
                        .AsNoTracking();
                    return (cancellationToken, query, id, param);
                })
                .Then("(embed) Parse json object", props =>
                {
                    var (cancellationToken, query, id, param) = props;
                    var json = param["embed"];
                    if (json == null) return (cancellationToken, query, id, embed: Array.Empty<string>());
                    try
                    {
                        var embed = JsonSerializer.Deserialize<string[]>(json, jsonSerializerOptions);
                        if (embed == null) throw new BadRequestException(
                            new()
                            {
                                { "embed", new[] { $"Invalid json array: {json}" } }
                            });

                        return (cancellationToken, query, id, embed);
                    }
                    catch (JsonException e)
                    {
                        throw new BadRequestException(
                           new()
                           {
                               { "embed", new[] { e.Message } }
                           });
                    }
                })
                .Then("(embed) Convert json property names to EF's", props =>
                {
                    var (cancellationToken, query, id, embed) = props;
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
                                        throw new BadRequestException(
                                            new()
                                            {
                                                { "embed", new[] { $"Invalid field name: {embedItem}" } }
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
                    return (cancellationToken, query, id, embed: convertedNames);
                })
                .Then("(embed) Apply embed", props =>
                {
                    var (cancellationToken, query, id, embed) = props;
                    var embedQuery = embed
                        .Aggregate(query, (acc, cur) => acc.Include(cur));
                    return (cancellationToken, query: embedQuery, id);
                })
                .Then("Get key's PropertyInfo", props =>
                {
                    var (cancellationToken, query, id) = props;
                    var dbSet = db.Set<TEntity>();
                    var propertyInfo = dbSet
                        .EntityType
                        .FindPrimaryKey()
                        .Properties
                        .Single()
                        .PropertyInfo;
                    return (cancellationToken, query, id, propertyInfo);
                })
                .Then("Convert id to key's value", props =>
                {
                    var (cancellationToken, query, id, propertyInfo) = props;
                    try
                    {
                        var value = JsonSerializer.Deserialize(id, propertyInfo.PropertyType, jsonSerializerOptions);
                        return (cancellationToken, query, value, propertyInfo);
                    }
                    catch (JsonException e)
                    {
                        throw new NotFoundException(
                            new()
                            {
                                { "id", new[] { e.Message } }
                            });
                    }
                })
                .Then("Create where expression", props =>
                {
                    var (cancellationToken, query, value, propertyInfo) = props;
                    var entityParameter = Expression.Parameter(typeof(TEntity), "entity");
                    var memberAccess = Expression.MakeMemberAccess(entityParameter, propertyInfo);
                    var valueExpression = Expression.Convert(Expression.Constant(value), propertyInfo.PropertyType);
                    var body = Expression.Equal(memberAccess, valueExpression);
                    var predicate = Expression.Lambda<Func<TEntity, bool>>(body, entityParameter);

                    return (cancellationToken, value, query: query.Where(predicate));
                })
                .Then("Run query", async props =>
                {
                    var (cancellationToken, value, query) = props;
                    var data = await query.SingleOrDefaultAsync(cancellationToken);
                    if (data == null)
                    {
                        throw new NotFoundException(new()
                        {
                            { "id", new[] { $"Id not found: {value}" } }
                        });
                    }
                    return data;
                })
                .Then("Create response", props =>
                {
                    var data = props;
                    var content = JsonContent.Create(data, null, jsonSerializerOptions);
                    var response = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = content
                    };
                    return response;
                })
                .Build();

            SetHandler(handler);
        }
    }
}
