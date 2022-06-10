using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using CloudCqs;
using CloudCqs.Command;
using EfRest.Extensions;
using Microsoft.EntityFrameworkCore;

namespace EfRest;

public class PatchCommand<TEntity, TKey> : Command<(TKey id, JsonElement patch)>
        where TEntity : class
{
    public PatchCommand(CloudCqsOptions option, DbContext db, JsonSerializerOptions jsonSerializerOptions, CancellationToken cancellationToken)
        : base(option)
    {
        var handler = new Handler()
            .Validate("Json string shoud be object",
            new()
            {
                ["body"] = new[] { "Json string shoud be a object" }
            },
            p => p.patch.ValueKind == JsonValueKind.Object)
            .Then("Get current entity", async p =>
            {
                var (idValue, patch) = p;
                var current = await db
                    .Set<TEntity>()
                    .FindAsync(new[] { idValue as object }, cancellationToken);
                if (current == null)
                {
                    throw new NotFoundException(new()
                    {
                        ["id"] = new[] { $"Not found: {idValue}" }
                    });
                }
                return (patch, current);
            })
            .Then("Get key's prop name", p =>
            {
                var (patch, current) = p;
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
                    throw new BadRequestException(new()
                    {
                        ["resource"] = new[] { $"Entity must have single primary key." }
                    });
                }
                return (patch, current, keyName);
            })
            .Then("Parse json", p =>
            {
                var (patch, current, keyName) = p;
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
                    throw new BadRequestException(new()
                    {
                        ["body"] = new[] { exception.Message }
                    });
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
                            throw new BadRequestException(new()
                            {
                                [propertyName] = new[] { "Invalid field name" }
                            });
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
                            throw new BadRequestException(new()
                            {
                                [propertyInfo.Name] = new[] { exception.Message }
                            });
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
                    propertyInfo.SetValue(current, value);
                }
            })
            .Then("Save to database", async _ =>
            {
                await db.SaveChangesAsync(cancellationToken);
            })
            .Build();

        SetHandler(handler);
    }
}

