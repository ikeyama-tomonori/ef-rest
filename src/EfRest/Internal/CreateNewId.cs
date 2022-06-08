using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using CloudCqs;
using CloudCqs.NewId;
using Microsoft.EntityFrameworkCore;

namespace EfRest.Internal;

internal class CreateNewId<TEntity, TKey> : NewId<TEntity, TKey>
    where TEntity : class
    where TKey : notnull
{
    public CreateNewId(CloudCqsOptions option, DbContext db, JsonSerializerOptions jsonSerializerOptions, CancellationToken cancellationToken) : base(option)
    {
        var handler = new Handler()
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
            .Then("Return id value", p =>
            {
                var (entity, propertyInfo) = p;
                var value = propertyInfo.GetValue(entity);
                if (value == null) throw new NullGuardException(nameof(value));
                return (TKey)value;
            })
            .Build();

        SetHandler(handler);
    }
}

