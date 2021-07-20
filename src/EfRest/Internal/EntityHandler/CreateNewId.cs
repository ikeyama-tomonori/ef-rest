using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using CloudCqs;
using CloudCqs.NewId;
using Microsoft.EntityFrameworkCore;

namespace EfRest.Internal.EntityHandler
{
    public class CreateNewId<TEntity> : NewId<HttpContent, string>
        where TEntity : class
    {
        public CreateNewId(CloudCqsOptions option, DbContext db, JsonSerializerOptions jsonSerializerOptions) : base(option)
        {
            var handler = new Handler()
                .Then("Deserialize json", async props =>
                {
                    var content = props;
                    try
                    {
                        var entity = await content.ReadFromJsonAsync<TEntity>(jsonSerializerOptions);
                        if (entity == null) throw new NullGuardException(nameof(entity));
                        return entity;
                    }
                    catch (JsonException exception)
                    {
                        throw new BadRequestException(new Dictionary<string, string[]>()
                        {
                            { "body", new[]{exception.Message } }
                        });
                    }
                })
                .Then("Validate by annotations", props =>
                {
                    var entity = props;
                    entity.Validate();
                    return entity;
                })
                .Then("Get key's PropertyInfo", props =>
                {
                    var entity = props;
                    var dbSet = db.Set<TEntity>();
                    var propertyInfo = dbSet
                        .EntityType
                        .FindPrimaryKey()
                        .Properties
                        .Single()
                        .PropertyInfo;
                    return (entity, propertyInfo);
                })
                .Then("Reset key as defualt", props =>
                {
                    var (entity, propertyInfo) = props;
                    var value =
                        propertyInfo.PropertyType.IsValueType
                        ? Activator.CreateInstance(propertyInfo.PropertyType)
                        : null;
                    propertyInfo.SetValue(entity, value);
                    return (entity, propertyInfo);
                })
                .Then("Add to DbSet", async props =>
                {
                    var (entity, propertyInfo) = props;
                    await db.Set<TEntity>().AddAsync(entity);
                    return (entity, propertyInfo);
                })
                .Then("Save to database", async props =>
                {
                    await db.SaveChangesAsync();
                    return props;
                })
                .Then("Serialize id value", props =>
                {
                    var (entity, propertyInfo) = props;
                    var value = propertyInfo.GetValue(entity);
                    var id = JsonSerializer.Serialize(value, jsonSerializerOptions);
                    return id;
                })
                .Build();
            SetHandler(handler);
        }
    }
}
