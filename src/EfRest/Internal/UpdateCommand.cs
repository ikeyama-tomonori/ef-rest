using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using CloudCqs;
using CloudCqs.Command;
using Microsoft.EntityFrameworkCore;

namespace EfRest.Internal;

internal class UpdateCommand<TEntity> : Command<(string id, string content)>
        where TEntity : class
{
    public UpdateCommand(CloudCqsOptions option, DbContext db, JsonSerializerOptions jsonSerializerOptions, CancellationToken cancellationToken)
        : base(option)
    {
        var handler = new Handler()
            .Then("Get key's PropertyInfo", p =>
            {
                var (id, content) = p;
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
                return (id, content, propertyInfo);
            })
            .Then("Get key's value", p =>
            {
                var (id, content, propertyInfo) = p;
                try
                {
                    var idValue = JsonSerializer.Deserialize(id, propertyInfo.PropertyType, jsonSerializerOptions);
                    return (idValue, content, propertyInfo);
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
                var (idValue, content, propertyInfo) = p;
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
                return (content, entity, keyName: propertyInfo.Name);
            })
            .Then("Parse json", p =>
           {
               var (content, entity, keyName) = p;
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
                           json: jsonProperty.Value.GetRawText()))
                       .ToArray();

                   return (entity, properties, keyName);
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
                var (entity, properties, keyName) = p;
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
                return (entity, propertyValues);
            })
            .Then("Update current values", p =>
            {
                var (entity, propertyValues) = p;
                foreach (var (propertyInfo, value) in propertyValues)
                {
                    propertyInfo.SetValue(entity, value);
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

