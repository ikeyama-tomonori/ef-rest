using System;
using System.Linq;
using CloudCqs;
using CloudCqs.NewId;
using Microsoft.EntityFrameworkCore;

namespace EfRest;

public class CreateNewId<TEntity, TKey> : NewId<TEntity, TKey>
    where TEntity : class
    where TKey : notnull
{
    public CreateNewId(CloudCqsOptions option, DbContext db)
        : base(option) => SetHandler(new Handler()
            .Then("Get key's PropertyInfo", _ =>
            {
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
                return propertyInfo;
            })
            .Then("Reset key as defualt", p =>
            {
                var propertyInfo = p;
                var entity = UseRequest();
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
                var cancellationToken = UseCancellationToken();
                await db.Set<TEntity>().AddAsync(entity, cancellationToken);
                return (entity, propertyInfo);
            })
            .Then("Save to database", async p =>
            {
                var (entity, propertyInfo) = p;
                var cancellationToken = UseCancellationToken();
                await db.SaveChangesAsync(cancellationToken);
                return (entity, propertyInfo);
            })
            .Then("Return id value", p =>
            {
                var (entity, propertyInfo) = p;
                var value = propertyInfo.GetValue(entity);
                if (value is TKey key) return key;
                throw new TypeGuardException(typeof(TKey), value);
            }));
}

