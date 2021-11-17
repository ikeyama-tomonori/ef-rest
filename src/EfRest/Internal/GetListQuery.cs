using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using CloudCqs;
using CloudCqs.Query;
using Microsoft.EntityFrameworkCore;

namespace EfRest.Internal
{
    internal class GetListQuery<TEntity>
        : Query<(NameValueCollection param, CancellationToken cancellationToken), HttpResponseMessage>
        where TEntity : class
    {
        public GetListQuery(CloudCqsOptions option, DbContext db, JsonSerializerOptions jsonSerializerOptions)
            : base(option)
        {
            var handler = new Handler()
                .Then("Create base query", p =>
                {
                    var (param, cancellationToken) = p;
                    var query = db
                        .Set<TEntity>()
                        .AsNoTracking();
                    return (cancellationToken, query, param);
                })
                .Then("(embed) Parse json object", p =>
                {
                    var (cancellationToken, query, param) = p;
                    var json = param["embed"];
                    if (json == null) return (cancellationToken, query, param, embed: Array.Empty<string>());
                    try
                    {
                        var embed = JsonSerializer.Deserialize<string[]>(json, jsonSerializerOptions);
                        if (embed == null) throw new BadRequestException(new()
                        {
                            ["embed"] = new[] { $"Invalid json array: {json}" }
                        });

                        return (cancellationToken, query, param, embed);
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
                    var (cancellationToken, query, param, embed) = p;
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
                    return (cancellationToken, query, param, embed: convertedNames);
                })
                .Then("(embed) Apply embed", p =>
                {
                    var (cancellationToken, query, param, embed) = p;
                    var embedQuery = embed
                        .Aggregate(query, (acc, cur) => acc.Include(cur));
                    return (cancellationToken, query: embedQuery, param);
                })
                .Then("(filter) Parse  json", p =>
                {
                    var (cancellationToken, query, param) = p;
                    var json = param["filter"];
                    if (json == null)
                    {
                        return (
                            cancellationToken,
                            query,
                            param,
                            Array.Empty<(string name, string json, JsonValueKind kind)>());
                    }
                    try
                    {
                        using var jsonDocument = JsonDocument.Parse(json);
                        if (jsonDocument.RootElement.ValueKind != JsonValueKind.Object)
                        {
                            throw new BadRequestException(new()
                            {
                                ["filter"] = new[] { $"Not object: {json}" }
                            });
                        }
                        var filters = jsonDocument
                            .RootElement
                            .EnumerateObject()
                            .Select(jsonProperty => (
                                name: jsonProperty.Name,
                                json: jsonProperty.Value.GetRawText(),
                                kind: jsonProperty.Value.ValueKind))
                            .ToArray();

                        return (cancellationToken, query, param, filters);
                    }
                    catch (JsonException e)
                    {
                        throw new BadRequestException(new()
                        {
                            ["filter"] = new[] { e.Message }
                        });
                    }
                })
                .Then("(filter) Divide by search or not", p =>
                {
                    var (cancellationToken, query, param, filters) = p;
                    var search = filters.FirstOrDefault(p => p.name.ToLower() == "q");
                    var rest = filters.Where(p => p.name.ToLower() != "q").ToArray();
                    return (cancellationToken, query, param, search, rest);
                })
                .Then("(filter) Make full text search expression", p =>
                {
                    var (cancellationToken, query, param, (name, json, _), rest) = p;
                    var entityParameter = Expression.Parameter(typeof(TEntity), "entity");
                    if (name != null && json != null)
                    {
                        var stringProperties = typeof(TEntity)
                            .GetProperties()
                            .Where(propertyInfo =>
                                propertyInfo.PropertyType == typeof(string)
                                && propertyInfo.GetCustomAttribute<NotMappedAttribute>() == null
                                && propertyInfo.GetCustomAttribute<JsonIgnoreAttribute>() == null)
                            .ToArray();
                        var value = JsonSerializer.Deserialize<string>(json, jsonSerializerOptions);
                        var searchExpression = !stringProperties.Any()
                            ? null
                            : stringProperties
                            .Select(p =>
                            {
                                var member = Expression.MakeMemberAccess(entityParameter, p);
                                Expression expression = Expression.Call(
                                    member,
                                    nameof(string.Contains),
                                    Type.EmptyTypes,
                                    Expression.Constant(value));
                                return expression;
                            })
                            .Aggregate((accumulator, current) => Expression.OrElse(accumulator, current));
                        return (cancellationToken, query, param, entityParameter, searchExpression, rest);
                    }
                    return (cancellationToken, query, param, entityParameter, default(Expression), rest);
                })
                .Then("(filter) Categorize properties", p =>
                {
                    var (cancellationToken, query, param, entityParameter, searchExpression, rest) = p;
                    var filters = rest
                        .Select(jsonProperty =>
                        {
                            var (propertyName, json, kind) = jsonProperty;
                            string baseName() => propertyName[0..propertyName.LastIndexOf('_')];

                            (Func<Expression, UnaryExpression, Expression> getExpression,
                            string name,
                            Func<Type, Type>? typeConverter) filter = propertyName switch
                            {
                                var name when name.EndsWith("_gt")
                                    => ((member, value) => Expression.GreaterThan(member, value), baseName(), null),
                                var name when name.EndsWith("_gte")
                                    => ((member, value) => Expression.GreaterThanOrEqual(member, value), baseName(), null),
                                var name when name.EndsWith("_lte")
                                    => ((member, value) => Expression.LessThanOrEqual(member, value), baseName(), null),
                                var name when name.EndsWith("_lt")
                                    => ((member, value) => Expression.LessThan(member, value), baseName(), null),
                                var name when name.EndsWith("_neq")
                                    => ((member, value) => Expression.NotEqual(member, value), baseName(), null),
                                _ => kind switch
                                {
                                    JsonValueKind.Array
                                        => ((member, value) =>
                                        {
                                            var contains = typeof(Enumerable)
                                                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                                                .First(m => m.Name == nameof(Enumerable.Contains)
                                                        && m.GetParameters().Length == 2)
                                                .MakeGenericMethod(member.Type);
                                            if (contains == null) throw new NullGuardException(nameof(contains));
                                            return Expression.Call(contains, value, member);
                                        },
                                        propertyName,
                                        t => t.MakeArrayType()),
                                    _ => ((member, value) => Expression.Equal(member, value), propertyName, null)
                                }
                            };

                            return (filter.getExpression, filter.name, json, filter.typeConverter);
                        })
                        .ToArray();
                    return (cancellationToken, query, param, entityParameter, searchExpression, filters);
                })
                .Then("(filter) Convert property name to member access", p =>
                {
                    var (cancellationToken, query, param, entityParameter, searchExpression, filters) = p;
                    var convertedFilters = filters
                        .Select(filter =>
                        {
                            var (getExpression, name, json, typeConverter) = filter;
                            var member = typeof(TEntity)
                                .GetMemberExpressionByJsonName(
                                    name,
                                    entityParameter,
                                    jsonSerializerOptions);
                            if (member == null)
                            {
                                throw new BadRequestException(new()
                                {
                                    ["filter"] = new[] { $"Property not found: {name}" }
                                });
                            }

                            return (getExpression, member, json, typeConverter);
                        })
                        .ToArray();
                    return (cancellationToken, query, param, entityParameter, searchExpression, convertedFilters);
                })
                .Then("(filter) Convert json string to expression", p =>
                {
                    var (cancellationToken, query, param, entityParameter, searchExpression, filters) = p;
                    var convertedFilters = filters
                        .Select(filter =>
                        {
                            var (getExpression, member, json, typeConverter) = filter;
                            var type = typeConverter == null ? member.Type : typeConverter(member.Type);
                            try
                            {
                                var value = JsonSerializer.Deserialize(json, type, jsonSerializerOptions);
                                var valueExpression = Expression.Convert(Expression.Constant(value), type);
                                return getExpression(member, valueExpression);
                            }
                            catch (JsonException e)
                            {
                                throw new BadRequestException(new()
                                {
                                    ["filter"] = new[] { e.Message }
                                });
                            }
                        })
                        .ToArray();
                    return (cancellationToken, query, param, entityParameter, searchExpression, convertedFilters);
                })
                .Then("(filter) Apply", p =>
                {
                    var (cancellationToken, query, param, entityParameter, searchExpression, filters) = p;
                    var mergedFilters = searchExpression == null
                        ? filters
                        : filters.Append(searchExpression);
                    if (!mergedFilters.Any())
                    {
                        return (cancellationToken, query, param);
                    }
                    var expressionBody = mergedFilters
                        .Aggregate((accumulator, current) => Expression.AndAlso(accumulator, current));
                    var expression = Expression.Lambda<Func<TEntity, bool>>(
                        expressionBody,
                        entityParameter);
                    if (expression == null) throw new NullGuardException(nameof(expression));

                    return (cancellationToken, query.Where(expression), param);
                })
                .Then("Count total", async p =>
                {
                    var (cancellationToken, query, param) = p;
                    var total = await query.LongCountAsync(cancellationToken);
                    return (cancellationToken, query, param, total);
                })
                .Then("(sort) Parse json", p =>
                {
                    var (cancellationToken, query, param, total) = p;
                    var json = param["sort"];
                    if (json == null)
                    {
                        (string field, string order)? sort = null;
                        return (cancellationToken, query, param, total, sort);
                    }
                    try
                    {
                        var sort = JsonSerializer.Deserialize<string[]>(json, jsonSerializerOptions);
                        if (sort == null || sort.Length != 2)
                        {
                            throw new BadRequestException(new()
                            {
                                ["sort"] = new[] { $"Invalid json array: {json}" }
                            });
                        }
                        return (cancellationToken, query, param, total, sort: (field: sort[0], order: sort[1]));
                    }
                    catch (JsonException e)
                    {
                        throw new BadRequestException(new()
                        {
                            ["sort"] = new[] { e.Message }
                        });
                    }
                })
                .Then("(sort) Get member access", p =>
                {
                    var (cancellationToken, query, param, total, sort) = p;
                    if (sort is (string field, string order))
                    {
                        var entityParameter = Expression.Parameter(typeof(TEntity), "entity");
                        var member = typeof(TEntity).GetMemberExpressionByJsonName(field, entityParameter, jsonSerializerOptions);
                        if (member == null)
                        {
                            throw new BadRequestException(new()
                            {
                                ["sort"] = new[] { $"Invalid sort field: ${field}" }
                            });
                        }
                        return (cancellationToken, query, param, total, sort: (member, order, entityParameter));
                    }
                    else
                    {
                        (Expression member, string order, ParameterExpression entityParameter)? sortForMenber = null;
                        return (cancellationToken, query, param, total, sort: sortForMenber);
                    }
                })
                .Then("(sort) Build sort for query", p =>
                {
                    var (cancellationToken, query, param, total, sort) = p;
                    if (sort is (Expression member, string order, ParameterExpression entityParameter))
                    {
                        var expression = Expression.Lambda<Func<TEntity, object>>(
                            Expression.Convert(member, typeof(object)),
                            entityParameter);
                        var sortedQuery = order switch
                        {
                            var o when o.Equals("asc", StringComparison.OrdinalIgnoreCase)
                                => query.OrderBy(expression),
                            var o when o.Equals("desc", StringComparison.OrdinalIgnoreCase)
                                => query.OrderByDescending(expression),
                            _ => throw new BadRequestException(new()
                            {
                                ["sort"] = new[] { $"Invalid sort order: ${order}" }
                            })
                        };
                        return (cancellationToken, query: sortedQuery, param, total);
                    }
                    return (cancellationToken, query, param, total);
                })
                .Then("(range) Apply", p =>
                {
                    var (cancellationToken, query, param, total) = p;
                    var json = param["range"];
                    if (json == null) return (cancellationToken, query, total, (start: 0, end: int.MaxValue));
                    try
                    {
                        var parsedRange = JsonSerializer.Deserialize<int[]>(json, jsonSerializerOptions);
                        if (parsedRange == null || parsedRange.Length != 2)
                        {
                            throw new BadRequestException(new()
                            {
                                ["range"] = new[] { $"Invalid json array: {json}" }
                            });
                        }
                        var range = (start: parsedRange[0], end: parsedRange[1]);
                        var rangedQuery = query.Skip(range.start).Take(range.end - range.start + 1);
                        return (cancellationToken, query: rangedQuery, total, range);
                    }
                    catch (JsonException e)
                    {
                        throw new BadRequestException(new()
                        {
                            ["range"] = new[] { e.Message }
                        });
                    }
                })
                .Then("Run query", async p =>
                {
                    var (cancellationToken, query, total, range) = p;
                    var data = await query.ToArrayAsync(cancellationToken);
                    return (range, total, data);
                })
                .Then("Build Content-Range header", p =>
                {
                    var (range, total, data) = p;
                    if (!data.Any())
                    {
                        var totalOnlyContentRange = new ContentRangeHeaderValue(total)
                        {
                            Unit = "items"
                        };
                        return (data, contentRange: totalOnlyContentRange);
                    }
                    var (start, end) = range;
                    var actualEnd = Math.Min(end, start + data.Length - 1);
                    var contentRange = new ContentRangeHeaderValue(start, actualEnd, total)
                    {
                        Unit = "items"
                    };
                    return (data, contentRange);
                })
                .Then("Determine the status", p =>
                {
                    var (data, contentRange) = p;
                    if (data.Length < contentRange.Length)
                    {
                        return (data, contentRange, status: HttpStatusCode.PartialContent);
                    }
                    return (data, contentRange, status: HttpStatusCode.OK);
                })
                .Then("Create response content", p =>
                {
                    var (data, contentRange, status) = p;
                    var content = JsonContent.Create(data, null, jsonSerializerOptions);
                    content.Headers.ContentRange = contentRange;
                    return (status, content);
                })
                .Then("Create response message", p =>
                {
                    var (status, content) = p;
                    var responseMessage = new HttpResponseMessage(status)
                    {
                        Content = content,
                    };
                    return responseMessage;
                })
                .Build();

            SetHandler(handler);
        }
    }
}
