using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using CloudCqs;
using CloudCqs.Command;
using EfRest.Extensions;
using Microsoft.EntityFrameworkCore;

namespace EfRest;

public class PatchCommand<TEntity, TKey> : Command<(TKey id, JsonElement patch)>
        where TEntity : class
{
    public PatchCommand(CloudCqsOptions option, DbContext db, JsonSerializerOptions jsonSerializerOptions)
        : base(option) => SetHandler(new Handler()
            .Validate(
                "Json string shoud be object",
                new("Json string shoud be a object", new[] { "body" }),
                _ => UseRequest().patch.ValueKind == JsonValueKind.Object)
            .Then("Get current entity", async p =>
            {
                var idValue = UseRequest().id;
                var cancellationToken = UseCancellationToken();
                var current = await db
                    .Set<TEntity>()
                    .FindAsync(new[] { idValue as object }, cancellationToken);
                if (current == null)
                {
                    throw new StatusCodeException(
                        HttpStatusCode.NotFound,
                        new($"Not found: {idValue}", new[] { "id" }));
                }
                return current;
            })
            .Then("Get key's prop name", p =>
            {
                var current = p;
                var keyName = db
                    .Set<TEntity>()
                    .EntityType
                    .FindPrimaryKey()?
                    .Properties
                    .SingleOrDefault()?
                    .PropertyInfo?
                    .Name;
                if (keyName == null)
                {
                    throw new StatusCodeException(
                        HttpStatusCode.BadRequest,
                        new($"Entity must have single primary key.", new[] { "resource" }));
                }
                return (current, keyName);
            })
            .Then("Parse json", p =>
            {
                var (current, keyName) = p;
                var patch = UseRequest().patch;
                try
                {
                    var properties = patch
                        .EnumerateObject()
                        .Select(jsonProperty => (
                            name: jsonProperty.Name,
                            json: jsonProperty.Value.GetRawText()))
                        .ToArray();

                    return (current, properties, keyName);
                }
                catch (JsonException exception)
                {
                    throw new StatusCodeException(
                        HttpStatusCode.BadRequest,
                        new(exception.Message, new[] { "body" }));
                }
            })
            .Then("Convert json properties", p =>
            {
                var (current, properties, keyName) = p;
                var propertyValues = properties
                    .Select(jsonProperty =>
                    {
                        var (propertyName, json) = jsonProperty;
                        var propertyInfo = typeof(TEntity)
                            .GetPropertyInfoByJsonName(propertyName, jsonSerializerOptions, typeof(JsonIgnoreAttribute));
                        if (propertyInfo == null)
                        {
                            throw new StatusCodeException(
                                HttpStatusCode.BadRequest,
                                new("Invalid field name", new[] { propertyName }));
                        }
                        try
                        {
                            var value = JsonSerializer.Deserialize(
                                json,
                                propertyInfo.PropertyType,
                                jsonSerializerOptions);
                            return (propertyInfo, value);
                        }
                        catch (JsonException exception)
                        {
                            throw new StatusCodeException(
                                HttpStatusCode.BadRequest,
                                new(exception.Message, new[] { propertyInfo.Name }));
                        }
                    })
                    .Where(p => p.propertyInfo.Name != keyName)
                    .ToArray();
                return (current, propertyValues);
            })
            .Then("Update current values", p =>
            {
                var (current, propertyValues) = p;
                foreach (var (propertyInfo, value) in propertyValues)
                {
                    propertyInfo.SetMethod?.Invoke(current, new[] { value });
                }
            })
            .Then("Save to database", async _ =>
            {
                var cancellationToken = UseCancellationToken();
                await db.SaveChangesAsync(cancellationToken);
            }));
}

