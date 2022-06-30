namespace EfRest;

using System;
using System.Linq;
using System.Net;
using CloudCqs;
using CloudCqs.NewId;
using Microsoft.EntityFrameworkCore;

public class CreateNewId<TEntity, TKey> : NewId<TEntity, TKey>
    where TEntity : class
    where TKey : notnull
{
    public CreateNewId(CloudCqsOptions option, DbContext db) : base(option)
    {
        var handler = new Handler()
            .Then(
                "Get key's PropertyInfo",
                _ =>
                {
                    var dbSet = db.Set<TEntity>();
                    var propertyInfo = dbSet.EntityType
                        .FindPrimaryKey()
                        ?.Properties.SingleOrDefault()
                        ?.PropertyInfo;
                    if (propertyInfo == null)
                    {
                        throw new StatusCodeException(
                            HttpStatusCode.BadRequest,
                            new($"Entity must have single primary key.", new[] { "resource" })
                        );
                    }
                    return propertyInfo;
                }
            )
            .Then(
                "Reset key as defualt",
                p =>
                {
                    var propertyInfo = p;
                    var entity = this.UseRequest();
                    var value = propertyInfo.PropertyType.IsValueType
                        ? Activator.CreateInstance(propertyInfo.PropertyType)
                        : null;
                    propertyInfo.SetValue(entity, value);
                    return (entity, propertyInfo);
                }
            )
            .Then(
                "Add to DbSet",
                async p =>
                {
                    var (entity, propertyInfo) = p;
                    var cancellationToken = this.UseCancellationToken();
                    await db.Set<TEntity>().AddAsync(entity, cancellationToken);
                    return (entity, propertyInfo);
                }
            )
            .Then(
                "Save to database",
                async p =>
                {
                    var (entity, propertyInfo) = p;
                    var cancellationToken = this.UseCancellationToken();
                    await db.SaveChangesAsync(cancellationToken);
                    return (entity, propertyInfo);
                }
            )
            .Then(
                "Return id value",
                p =>
                {
                    var (entity, propertyInfo) = p;
                    var value = propertyInfo.GetValue(entity);
                    if (value is TKey key)
                    {
                        return key;
                    }
                    throw new TypeGuardException(typeof(TKey), value);
                }
            );

        this.SetHandler(handler);
    }
}
