using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using CloudCqs;
using CloudCqs.Command;
using Microsoft.EntityFrameworkCore;

namespace EfRest.Internal.EntityHandler
{
    internal class PatchCommand<TEntity> : Command<(string id, HttpContent content)>
        where TEntity : class
    {
        public PatchCommand(CloudCqsOptions option, DbContext db, JsonSerializerOptions jsonSerializerOptions)
            : base(option)
        {
            var handler = new Handler()
                .Then("Get key's PropertyInfo", props =>
                {
                    var (id, content) = props;
                    var propertyInfo = db
                        .Set<TEntity>()
                        .EntityType
                        .FindPrimaryKey()
                        .Properties
                        .Single()
                        .PropertyInfo;
                    return (id, content, propertyInfo);
                })
                .Then("Get key's value", props =>
                {
                    var (id, content, propertyInfo) = props;
                    try
                    {
                        var idValue = JsonSerializer.Deserialize(id, propertyInfo.PropertyType, jsonSerializerOptions);
                        return (idValue, content, propertyInfo);
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
                .Then("Get current entity", async props =>
                {
                    var (idValue, content, propertyInfo) = props;
                    var entity = await db
                        .Set<TEntity>()
                        .FindAsync(idValue);
                    if (entity == null)
                    {
                        throw new NotFoundException(
                            new()
                            {
                                { "id", new[] { $"Not found: {idValue}" } }
                            });
                    }
                    return (content, entity, keyName: propertyInfo.Name);
                })
                .Then("Parse json", async props =>
                {
                    var (content, entity, keyName) = props;
                    try
                    {
                        var json = await content.ReadAsStringAsync();
                        using var jsonDocument = JsonDocument.Parse(json);
                        if (jsonDocument.RootElement.ValueKind != JsonValueKind.Object)
                        {
                            throw new BadRequestException(
                                new()
                                {
                                    { "body", new[] { $"Not object: {json}" } }
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

                        return (entity, properties, keyName);
                    }
                    catch (JsonException exception)
                    {
                        throw new BadRequestException(new Dictionary<string, string[]>()
                        {
                            { "body", new[]{exception.Message } }
                        });
                    }
                })
                .Then("Convert json properties", props =>
                {
                    var (entity, properties, keyName) = props;
                    var propertyValues = properties
                        .Select(jsonProperty =>
                        {
                            var (propertyName, json, kind) = jsonProperty;
                            var propertyInfo = typeof(TEntity)
                                .GetPropertyInfo(propertyName, jsonSerializerOptions);
                            if (propertyInfo == null)
                            {
                                throw new BadRequestException(new()
                                {
                                    { propertyName, new[] { "Invalid field name" } }
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
                                    { propertyInfo.Name, new[] { exception.Message } }
                                });
                            }
                        })
                        .Where(p => p.propertyInfo.Name != keyName)
                        .ToArray();
                    return (entity, propertyValues);
                })
                .Then("Update current values", props =>
                {
                    var (entity, propertyValues) = props;
                    foreach (var (propertyInfo, value) in propertyValues)
                    {
                        propertyInfo.SetValue(entity, value);
                    }
                    return entity;
                })
                .Then("Validate by annotations", props =>
                {
                    var entity = props;
                    entity.Validate();
                })
                .Then("Save to database", async () =>
                {
                    await db.SaveChangesAsync();
                })
                .Build();

            SetHandler(handler);
        }
    }
}
