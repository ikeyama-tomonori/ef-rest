using System.Linq;
using System.Threading;
using CloudCqs;
using CloudCqs.Command;
using Microsoft.EntityFrameworkCore;

namespace EfRest.Internal;

internal class UpdateCommand<TEntity, TKey> : Command<(TKey id, TEntity entity)>
        where TEntity : class
{
    public UpdateCommand(CloudCqsOptions option, DbContext db, CancellationToken cancellationToken)
        : base(option)
    {
        var handler = new Handler()
            .Then("Get current entity", async p =>
            {
                var (idValue, updated) = p;
                var current = await db
                    .Set<TEntity>()
                    .FindAsync(new[] { idValue as object }, cancellationToken);
                if (current == null)
                {
                    throw new NotFoundException(new()
                    {
                        ["id"] = new[] { $"Not found: {idValue}" }
                    });
                }
                return (updated, current);
            })
            .Then("Get key's prop name", p =>
            {
                var (updated, current) = p;
                var keyPropertyName = db
                    .Set<TEntity>()
                    .EntityType
                    .FindPrimaryKey()?
                    .Properties
                    .SingleOrDefault()?
                    .PropertyInfo?
                    .Name;
                if (keyPropertyName == null)
                {
                    throw new BadRequestException(new()
                    {
                        ["resource"] = new[] { $"Entity must have single primary key." }
                    });
                }
                return (updated, current, keyPropertyName);
            })
            .Then("Update current entity", p =>
            {
                var (updated, current, keyPropertyName) = p;
                var propertyInfos = typeof(TEntity)
                    .GetProperties()
                    .Where(p => p.Name != keyPropertyName);
                foreach (var propertyInfo in propertyInfos)
                {
                    var newValue = propertyInfo.GetValue(updated);
                    propertyInfo.SetValue(current, newValue);
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

