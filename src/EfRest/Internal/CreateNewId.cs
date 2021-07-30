using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using CloudCqs;
using CloudCqs.NewId;
using Microsoft.EntityFrameworkCore;

namespace EfRest.Internal
{
    internal class CreateNewId<TEntity> : NewId<(string item, CancellationToken cancellationToken), string>
        where TEntity : class
    {
        public CreateNewId(CloudCqsOptions option, DbContext db, JsonSerializerOptions jsonSerializerOptions) : base(option)
        {
            var handler = new Handler()
                .Then("Deserialize json", props =>
                {
                    var (content, cancellationToken) = props;
                    try
                    {
                        var entity = JsonSerializer.Deserialize<TEntity>(
                            content,
                            jsonSerializerOptions);
                        if (entity == null) throw new NullGuardException(nameof(entity));
                        return (cancellationToken, entity);
                    }
                    catch (JsonException exception)
                    {
                        throw new BadRequestException(new()
                        {
                            ["body"] = new[] { exception.Message }
                        });
                    }
                })
                .Then("Validate by annotations", props =>
                {
                    var (cancellationToken, entity) = props;
                    entity.Validate();
                    return (cancellationToken, entity);
                })
                .Then("Get key's PropertyInfo", props =>
                {
                    var (cancellationToken, entity) = props;
                    var dbSet = db.Set<TEntity>();
                    var propertyInfo = dbSet
                        .EntityType
                        .FindPrimaryKey()
                        .Properties
                        .Single()
                        .PropertyInfo;
                    return (cancellationToken, entity, propertyInfo);
                })
                .Then("Reset key as defualt", props =>
                {
                    var (cancellationToken, entity, propertyInfo) = props;
                    var value =
                        propertyInfo.PropertyType.IsValueType
                        ? Activator.CreateInstance(propertyInfo.PropertyType)
                        : null;
                    propertyInfo.SetValue(entity, value);
                    return (cancellationToken, entity, propertyInfo);
                })
                .Then("Add to DbSet", async props =>
                {
                    var (cancellationToken, entity, propertyInfo) = props;
                    await db.Set<TEntity>().AddAsync(entity, cancellationToken);
                    return (cancellationToken, entity, propertyInfo);
                })
                .Then("Save to database", async props =>
                {
                    var (cancellationToken, entity, propertyInfo) = props;
                    await db.SaveChangesAsync(cancellationToken);
                    return (entity, propertyInfo);
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
