using CloudCqs;
using CloudCqs.Query;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace EfRest.Internal.EntityHandler
{
    internal class GetOneQuery<TEntity> : Query<(string id, NameValueCollection param), HttpResponseMessage>
        where TEntity : class
    {
        public GetOneQuery(CloudCqsOptions option, DbContext db, JsonSerializerOptions jsonSerializerOptions) : base(option)
        {
            var handler = new Handler()
                .Then("Create base query", props =>
                {
                    var (id, param) = props;
                    var query = db
                        .Set<TEntity>()
                        .AsNoTracking();
                    return (query, id, param);
                })
                .Then("(embed) Parse json object", props =>
                {
                    var (query, id, param) = props;
                    var json = param["embed"];
                    if (json == null) return (query, id, embed: Array.Empty<string>());
                    try
                    {
                        var embed = JsonSerializer.Deserialize<string[]>(json, jsonSerializerOptions);
                        if (embed == null) throw new BadRequestException(
                            new()
                            {
                                { "embed", new[] { $"Invalid json array: {json}" } }
                            });

                        return (query, id, embed);
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
                    var (query, id, embed) = props;
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
                                        .GetPropertyInfo(
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
                    return (query, id, embed: convertedNames);
                })
                .Then("(embed) Apply embed", props =>
                {
                    var (query, id, embed) = props;
                    var embedQuery = embed
                        .Aggregate(query, (acc, cur) => acc.Include(cur));
                    return (query: embedQuery, id);
                })
                .Then("Get key's PropertyInfo", props =>
                {
                    var (query, id) = props;
                    var dbSet = db.Set<TEntity>();
                    var propertyInfo = dbSet
                        .EntityType
                        .FindPrimaryKey()
                        .Properties
                        .Single()
                        .PropertyInfo;
                    return (query, id, propertyInfo);
                })
                .Then("Convert id to key's value", props =>
                {
                    var (query, id, propertyInfo) = props;
                    try
                    {
                        var value = JsonSerializer.Deserialize(id, propertyInfo.PropertyType, jsonSerializerOptions);
                        return (query, value, propertyInfo);
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
                    var (query, value, propertyInfo) = props;
                    var entityParameter = Expression.Parameter(typeof(TEntity), "entity");
                    var memberAccess = Expression.MakeMemberAccess(entityParameter, propertyInfo);
                    var valueExpression = Expression.Convert(Expression.Constant(value), propertyInfo.PropertyType);
                    var body = Expression.Equal(memberAccess, valueExpression);
                    var predicate = Expression.Lambda<Func<TEntity, bool>>(body, entityParameter);

                    return (value, query: query.Where(predicate));
                })
                .Then("Run query", async props =>
                {
                    var (value, query) = props;
                    var data = await query.SingleOrDefaultAsync();
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
