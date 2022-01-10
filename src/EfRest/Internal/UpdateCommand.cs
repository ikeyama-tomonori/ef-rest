using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using CloudCqs;
using CloudCqs.Command;
using Microsoft.EntityFrameworkCore;

namespace EfRest.Internal;

internal class UpdateCommand<TEntity> : Command<(string id, string content, CancellationToken cancellationToken)>
        where TEntity : class
{
    public UpdateCommand(CloudCqsOptions option, DbContext db, JsonSerializerOptions jsonSerializerOptions)
        : base(option)
    {
        var handler = new Handler()
            .Then("Get key's PropertyInfo", p =>
            {
                var (id, content, cancellationToken) = p;
                var propertyInfo = db
                    .Set<TEntity>()
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
                return (cancellationToken, id, content, propertyInfo);
            })
            .Then("Get key's value", p =>
            {
                var (cancellationToken, id, content, propertyInfo) = p;
                try
                {
                    var idValue = JsonSerializer.Deserialize(id, propertyInfo.PropertyType, jsonSerializerOptions);
                    return (cancellationToken, idValue, content, propertyInfo);
                }
                catch (JsonException e)
                {
                    throw new NotFoundException(new()
                    {
                        ["id"] = new[] { e.Message }
                    });
                }
            })
            .Then("Get current entity", async p =>
            {
                var (cancellationToken, idValue, content, propertyInfo) = p;
                var entity = await db
                    .Set<TEntity>()
                    .FindAsync(new[] { idValue }, cancellationToken);
                if (entity == null)
                {
                    throw new NotFoundException(new()
                    {
                        ["id"] = new[] { $"Not found: {idValue}" }
                    });
                }
                return (cancellationToken, content, entity, keyName: propertyInfo.Name);
            })
            .Then("Parse json", p =>
           {
               var (cancellationToken, content, entity, keyName) = p;
               try
               {
                   var json = content;
                   using var jsonDocument = JsonDocument.Parse(json);
                   if (jsonDocument.RootElement.ValueKind != JsonValueKind.Object)
                   {
                       throw new BadRequestException(new()
                       {
                           ["body"] = new[] { $"Not json object: {json}" }
                       });
                   }
                   var properties = jsonDocument
                       .RootElement
                       .EnumerateObject()
                       .Select(jsonProperty => (
                           name: jsonProperty.Name,
                           json: jsonProperty.Value.GetRawText(),
                           kind: jsonProperty.Value.ValueKind))
                       .ToArray();

                   return (cancellationToken, entity, properties, keyName);
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
                var (cancellationToken, entity, properties, keyName) = p;
                var propertyValues = properties
                    .Select(jsonProperty =>
                    {
                        var (propertyName, json, kind) = jsonProperty;
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
                return (cancellationToken, entity, propertyValues);
            })
            .Then("Update current values", p =>
            {
                var (cancellationToken, entity, propertyValues) = p;
                foreach (var (propertyInfo, value) in propertyValues)
                {
                    propertyInfo.SetValue(entity, value);
                }
                return (cancellationToken, entity);
            })
            .Then("Validate by annotations", p =>
            {
                var (cancellationToken, entity) = p;
                entity.Validate();
                return (cancellationToken, entity);
            })
            .Then("Save to database", async p =>
            {
                var (cancellationToken, entity) = p;
                await db.SaveChangesAsync(cancellationToken);
            })
            .Build();

        SetHandler(handler);
    }
}

