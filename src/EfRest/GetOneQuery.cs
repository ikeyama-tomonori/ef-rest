namespace EfRest;

using System.Linq.Expressions;
using System.Net;
using System.Text.Json;
using CloudCqs;
using CloudCqs.Query;
using EfRest.Extensions;
using Microsoft.EntityFrameworkCore;

public class GetOneQuery<TEntity, TKey> : Query<(TKey Id, string? Embed), TEntity>
    where TEntity : class
{
    public GetOneQuery(
        CloudCqsOptions option,
        DbContext db,
        JsonSerializerOptions jsonSerializerOptions
    ) : base(option)
    {
        var handler = new Handler()
            .Then(
                "Create base query",
                _ =>
                {
                    var query = db.Set<TEntity>().AsNoTracking();
                    return query;
                }
            )
            .Then(
                "(embed) Parse json object",
                p =>
                {
                    var query = p;
                    var json = this.UseRequest().Embed;
                    if (json == null)
                    {
                        return (query, embed: Array.Empty<string>());
                    }
                    try
                    {
                        var embed = JsonSerializer.Deserialize<string[]>(
                            json,
                            jsonSerializerOptions
                        );
                        if (embed == null)
                        {
                            throw new StatusCodeException(
                                HttpStatusCode.BadRequest,
                                new($"Invalid json array: {json}", new[] { "embed" })
                            );
                        }
                        return (query, embed);
                    }
                    catch (JsonException e)
                    {
                        throw new StatusCodeException(
                            HttpStatusCode.BadRequest,
                            new(e.Message, new[] { "embed" })
                        );
                    }
                }
            )
            .Then(
                "(embed) Convert json property names to EF's",
                p =>
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
                                        var propertyInfo = type.GetPropertyInfoByJsonName(
                                            current,
                                            jsonSerializerOptions
                                        );
                                        if (propertyInfo == null)
                                        {
                                            throw new StatusCodeException(
                                                HttpStatusCode.BadRequest,
                                                new(
                                                    $"Invalid field name: {embedItem}",
                                                    new[] { "embed" }
                                                )
                                            );
                                        }
                                        var newNameList = nameList
                                            .Append(propertyInfo.Name)
                                            .ToArray();
                                        var propType = propertyInfo.PropertyType;
                                        if (
                                            propType.IsGenericType
                                            && propType
                                                .GetInterfaces()
                                                .Any(
                                                    i =>
                                                        i.IsGenericType
                                                        && i.GetGenericTypeDefinition()
                                                            == typeof(IEnumerable<>)
                                                )
                                        )
                                        {
                                            var newType = propType.GetGenericArguments().First();
                                            return (newNameList, newType);
                                        }
                                        return (newNameList, propType);
                                    }
                                )
                                .nameList;
                            var includeName = string.Join('.', names);
                            return includeName;
                        })
                        .ToArray();
                    return (query, embed: convertedNames);
                }
            )
            .Then(
                "(embed) Apply embed",
                p =>
                {
                    var (query, embed) = p;
                    var embedQuery = embed.Aggregate(query, (acc, cur) => acc.Include(cur));
                    return embedQuery;
                }
            )
            .Then(
                "Get key's PropertyInfo",
                p =>
                {
                    var query = p;
                    var dbSet = db.Set<TEntity>();
                    var propertyInfo = dbSet.EntityType
                        .FindPrimaryKey()
                        ?.Properties.SingleOrDefault()
                        ?.PropertyInfo;
                    if (propertyInfo == null)
                    {
                        throw new StatusCodeException(
                            HttpStatusCode.BadRequest,
                            new($"Entity must have single primary key.", new[] { "resource" })
                        );
                    }
                    return (query, propertyInfo);
                }
            )
            .Then(
                "Create where expression",
                p =>
                {
                    var (query, propertyInfo) = p;
                    var id = this.UseRequest().Id;
                    var entityParameter = Expression.Parameter(typeof(TEntity), "entity");
                    var memberAccess = Expression.MakeMemberAccess(entityParameter, propertyInfo);
                    var valueExpression = Expression.Convert(
                        Expression.Constant(id),
                        propertyInfo.PropertyType
                    );
                    var body = Expression.Equal(memberAccess, valueExpression);
                    var predicate = Expression.Lambda<Func<TEntity, bool>>(body, entityParameter);

                    return (id, query: query.Where(predicate));
                }
            )
            .Then(
                "Run query",
                async p =>
                {
                    var (id, query) = p;
                    var cancellationToken = this.UseCancellationToken();
                    var data = await query.SingleOrDefaultAsync(cancellationToken);
                    if (data == null)
                    {
                        throw new StatusCodeException(
                            HttpStatusCode.NotFound,
                            new($"Not found: {id}", new[] { "id" })
                        );
                    }
                    return data;
                }
            );

        this.SetHandler(handler);
    }
}
