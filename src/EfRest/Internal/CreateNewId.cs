using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using CloudCqs;
using CloudCqs.NewId;
using Microsoft.EntityFrameworkCore;

namespace EfRest.Internal;

internal class CreateNewId<TEntity> : NewId<string, string>
        where TEntity : class
{
    public CreateNewId(CloudCqsOptions option, DbContext db, JsonSerializerOptions jsonSerializerOptions, CancellationToken cancellationToken) : base(option)
    {
        var handler = new Handler()
            .Then("Deserialize json", p =>
            {
                var content = p;
                try
                {
                    var entity = JsonSerializer.Deserialize<TEntity>(
                        content,
                        jsonSerializerOptions);
                    if (entity == null) throw new NullGuardException(nameof(entity));
                    return entity;
                }
                catch (JsonException exception)
                {
                    throw new BadRequestException(new()
                    {
                        ["body"] = new[] { exception.Message }
                    });
                }
            })
            .Then("Get key's PropertyInfo", p =>
            {
                var entity = p;
                var dbSet = db.Set<TEntity>();
                var propertyInfo = dbSet
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
                return (entity, propertyInfo);
            })
            .Then("Reset key as defualt", p =>
            {
                var (entity, propertyInfo) = p;
                var value =
                    propertyInfo.PropertyType.IsValueType
                    ? Activator.CreateInstance(propertyInfo.PropertyType)
                    : null;
                propertyInfo.SetValue(entity, value);
                return (entity, propertyInfo);
            })
            .Then("Add to DbSet", async p =>
            {
                var (entity, propertyInfo) = p;
                await db.Set<TEntity>().AddAsync(entity, cancellationToken);
                return (entity, propertyInfo);
            })
            .Then("Save to database", async p =>
            {
                var (entity, propertyInfo) = p;
                await db.SaveChangesAsync(cancellationToken);
                return (entity, propertyInfo);
            })
            .Then("Serialize id value", p =>
            {
                var (entity, propertyInfo) = p;
                var value = propertyInfo.GetValue(entity);
                var id = JsonSerializer.Serialize(value, jsonSerializerOptions);
                return id;
            })
            .Build();
        SetHandler(handler);
    }
}

