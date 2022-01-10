using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using CloudCqs;
using CloudCqs.NewId;
using Microsoft.EntityFrameworkCore;

namespace EfRest.Internal;

internal class CreateNewId<TEntity> : NewId<(string item, CancellationToken cancellationToken), string>
        where TEntity : class
{
    public CreateNewId(CloudCqsOptions option, DbContext db, JsonSerializerOptions jsonSerializerOptions) : base(option)
    {
        var handler = new Handler()
            .Then("Deserialize json", p =>
            {
                var (content, cancellationToken) = p;
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
            .Then("Validate by annotations", p =>
            {
                var (cancellationToken, entity) = p;
                entity.Validate();
                return (cancellationToken, entity);
            })
            .Then("Get key's PropertyInfo", p =>
            {
                var (cancellationToken, entity) = p;
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
                return (cancellationToken, entity, propertyInfo);
            })
            .Then("Reset key as defualt", p =>
            {
                var (cancellationToken, entity, propertyInfo) = p;
                var value =
                    propertyInfo.PropertyType.IsValueType
                    ? Activator.CreateInstance(propertyInfo.PropertyType)
                    : null;
                propertyInfo.SetValue(entity, value);
                return (cancellationToken, entity, propertyInfo);
            })
            .Then("Add to DbSet", async p =>
            {
                var (cancellationToken, entity, propertyInfo) = p;
                await db.Set<TEntity>().AddAsync(entity, cancellationToken);
                return (cancellationToken, entity, propertyInfo);
            })
            .Then("Save to database", async p =>
            {
                var (cancellationToken, entity, propertyInfo) = p;
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

