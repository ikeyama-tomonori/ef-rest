using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Text.Json;
using CloudCqs;
using CloudCqs.Query;
using EfRest.Extensions;
using Microsoft.EntityFrameworkCore;

namespace EfRest;

public class GetListQuery<TEntity>
        : Query<(string? embed, string? filter, string? sort, string? range), (TEntity[] data, int total, (int first, int last)? range)>
        where TEntity : class
{
    public GetListQuery(CloudCqsOptions option, DbContext db, JsonSerializerOptions jsonSerializerOptions)
        : base(option) => SetHandler(new Handler()
            .Then("Create base query", _ =>
            {
                var query = db
                    .Set<TEntity>()
                    .AsNoTracking();
                return query;
            })
            .Then("(embed) Parse json object", p =>
            {
                var query = p;
                var json = UseRequest().embed;
                if (string.IsNullOrEmpty(json)) return (query, embed: Array.Empty<string>());
                try
                {
                    var embed = JsonSerializer.Deserialize<string[]>(json, jsonSerializerOptions);
                    if (embed == null)
                    {
                        throw new StatusCodeException(
                            HttpStatusCode.BadRequest,
                            new($"Invalid json array: {json}", new[] { "embed" }));
                    }
                    return (query, embed);
                }
                catch (JsonException e)
                {
                    throw new StatusCodeException(
                        HttpStatusCode.BadRequest,
                        new(e.Message, new[] { "embed" }));
                }
            })
            .Then("(embed) Convert json property names to EF's", p =>
            {
                var (query, embed) = p;
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
                                    throw new StatusCodeException(
                                        HttpStatusCode.BadRequest,
                                        new($"Invalid field name: {embedItem}", new[] { "embed" }));
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
                return (query, embed: convertedNames);
            })
            .Then("(embed) Apply embed", p =>
            {
                var (query, embed) = p;
                var embedQuery = embed
                    .Aggregate(query, (acc, cur) => acc.Include(cur));
                return embedQuery;
            })
            .Then("(filter) Parse  json", p =>
            {
                var query = p;
                var json = UseRequest().filter;
                if (string.IsNullOrEmpty(json))
                {
                    return (query,
                        filters: Array.Empty<(string name, string json, JsonValueKind kind)>());
                }
                try
                {
                    using var jsonDocument = JsonDocument.Parse(json);
                    if (jsonDocument.RootElement.ValueKind != JsonValueKind.Object)
                    {
                        throw new StatusCodeException(
                            HttpStatusCode.BadRequest,
                            new($"Not object: {json}", new[] { "filter" }));
                    }
                    var filters = jsonDocument
                        .RootElement
                        .EnumerateObject()
                        .Select(jsonProperty => (
                            name: jsonProperty.Name,
                            json: jsonProperty.Value.GetRawText(),
                            kind: jsonProperty.Value.ValueKind))
                        .ToArray();

                    return (query, filters);
                }
                catch (JsonException e)
                {
                    throw new StatusCodeException(
                        HttpStatusCode.BadRequest,
                        new(e.Message, new[] { "filter" }));
                }
            })
            .Then("(filter) Divide by search or not", p =>
            {
                var (query, filters) = p;
                var search = filters.FirstOrDefault(p => p.name.ToLower() == "q");
                var rest = filters.Where(p => p.name.ToLower() != "q").ToArray();
                return (query, search, rest);
            })
            .Then("(filter) Make full text search expression", p =>
            {
                var (query, (name, json, _), rest) = p;
                var entityParameter = Expression.Parameter(typeof(TEntity), "entity");
                if (name != null && json != null)
                {
                    var stringProperties = db
                        .Set<TEntity>()
                        .EntityType
                        .GetProperties()
                        .Select(prop => prop.PropertyInfo)
                        .Where(propertyInfo =>
                            propertyInfo is not null
                            && propertyInfo.PropertyType == typeof(string));

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
                    return (query, entityParameter, searchExpression, rest);
                }
                return (query, entityParameter, default(Expression), rest);
            })
            .Then("(filter) Categorize properties", p =>
            {
                var (query, entityParameter, searchExpression, rest) = p;
                var filters = rest
                    .Select(jsonProperty =>
                    {
                        var (propertyName, json, kind) = jsonProperty;
                        var baseName = () => propertyName[0..propertyName.LastIndexOf('_')];

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
                return (query, entityParameter, searchExpression, filters);
            })
            .Then("(filter) Convert property name to member access", p =>
            {
                var (query, entityParameter, searchExpression, filters) = p;
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
                            throw new StatusCodeException(
                                HttpStatusCode.BadRequest,
                                new($"Property not found: {name}", new[] { "filter" }));
                        }

                        return (getExpression, member, json, typeConverter);
                    })
                    .ToArray();
                return (query, entityParameter, searchExpression, convertedFilters);
            })
            .Then("(filter) Convert json string to expression", p =>
            {
                var (query, entityParameter, searchExpression, filters) = p;
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
                            throw new StatusCodeException(
                                HttpStatusCode.BadRequest,
                                new(e.Message, new[] { "filter" }));
                        }
                    })
                    .ToArray();
                return (query, entityParameter, searchExpression, convertedFilters);
            })
            .Then("(filter) Apply", p =>
            {
                var (query, entityParameter, searchExpression, filters) = p;
                var mergedFilters = searchExpression == null
                    ? filters
                    : filters.Append(searchExpression);
                if (!mergedFilters.Any())
                {
                    return query;
                }
                var expressionBody = mergedFilters
                    .Aggregate((accumulator, current) => Expression.AndAlso(accumulator, current));
                var expression = Expression.Lambda<Func<TEntity, bool>>(
                    expressionBody,
                    entityParameter);
                if (expression == null) throw new NullGuardException(nameof(expression));

                return query.Where(expression);
            })
            .Then("Count total", async p =>
            {
                var query = p;
                var cancellationToken = UseCancellationToken();
                var total = await query.CountAsync(cancellationToken);
                return (query, total);
            })
            .Then("(sort) Parse json", p =>
            {
                var (query, total) = p;
                var json = UseRequest().sort;
                if (string.IsNullOrEmpty(json))
                {
                    (string field, string order)? sort = null;
                    return (query, total, sort);
                }
                try
                {
                    var sort = JsonSerializer.Deserialize<string[]>(json, jsonSerializerOptions);
                    if (sort == null || sort.Length != 2)
                    {
                        throw new StatusCodeException(
                            HttpStatusCode.BadRequest,
                            new($"Invalid json array: {json}", new[] { "sort" }));
                    }
                    return (query, total, sort: (field: sort[0], order: sort[1]));
                }
                catch (JsonException e)
                {
                    throw new StatusCodeException(
                       HttpStatusCode.BadRequest,
                       new(e.Message, new[] { "sort" }));
                }
            })
            .Then("(sort) Get member access", p =>
            {
                var (query, total, sort) = p;
                if (sort is (string field, string order))
                {
                    var entityParameter = Expression.Parameter(typeof(TEntity), "entity");
                    var member = typeof(TEntity).GetMemberExpressionByJsonName(field, entityParameter, jsonSerializerOptions);
                    if (member == null)
                    {
                        throw new StatusCodeException(
                            HttpStatusCode.BadRequest,
                            new($"Invalid sort field: ${field}", new[] { "sort" }));
                    }
                    return (query, total, sort: (member, order, entityParameter));
                }
                else
                {
                    (Expression member, string order, ParameterExpression entityParameter)? sortForMenber = null;
                    return (query, total, sort: sortForMenber);
                }
            })
            .Then("(sort) Build sort for query", p =>
            {
                var (query, total, sort) = p;
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
                        _ => throw new StatusCodeException(
                                HttpStatusCode.BadRequest,
                                new($"Invalid sort order: ${order}", new[] { "sort" }))
                    };
                    return (query: sortedQuery, total);
                }
                return (query, total);
            })
            .Then("(range) Apply", p =>
            {
                var (query, total) = p;
                var json = UseRequest().range;
                if (string.IsNullOrEmpty(json)) return (query, total, (start: 0, end: int.MaxValue));
                try
                {
                    var parsedRange = JsonSerializer.Deserialize<int[]>(json, jsonSerializerOptions);
                    if (parsedRange == null || parsedRange.Length != 2)
                    {
                        throw new StatusCodeException(
                           HttpStatusCode.BadRequest,
                           new($"Invalid json array: {json}", new[] { "range" }));
                    }
                    var range = (start: parsedRange[0], end: parsedRange[1]);
                    var rangedQuery = query.Skip(range.start).Take(range.end - range.start + 1);
                    return (query: rangedQuery, total, range);
                }
                catch (JsonException e)
                {
                    throw new StatusCodeException(
                       HttpStatusCode.BadRequest,
                       new(e.Message, new[] { "range" }));
                }
            })
            .Then("Run query", async p =>
            {
                var (query, total, range) = p;
                var cancellationToken = UseCancellationToken();
                var data = await query.ToArrayAsync(cancellationToken);
                return (range, total, data);
            })
            .Then("Calculate response range", p =>
            {
                var (range, total, data) = p;
                (int first, int last)? responseRange = data.Any()
                    ? (range.start, Math.Min(range.end, range.start + data.Length - 1))
                    : null;
                return (data, total, range: responseRange);
            }));
}

