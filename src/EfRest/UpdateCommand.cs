using System.Linq;
using CloudCqs;
using CloudCqs.Command;
using Microsoft.EntityFrameworkCore;

namespace EfRest;

public class UpdateCommand<TEntity, TKey> : Command<(TKey id, TEntity entity)>
        where TEntity : class
{
    public UpdateCommand(CloudCqsOptions option, DbContext db)
        : base(option) => SetHandler(new Handler()
            .Then("Get current entity", async p =>
            {
                var idValue = UseRequest().id;
                var cancellationToken = UseCancellationToken();
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
                return current;
            })
            .Then("Get key's prop name", p =>
            {
                var current = p;
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
                return (current, keyPropertyName);
            })
            .Then("Update current entity", p =>
            {
                var (current, keyPropertyName) = p;
                var propertyInfos = typeof(TEntity)
                    .GetProperties()
                    .Where(p => p.Name != keyPropertyName);
                foreach (var propertyInfo in propertyInfos)
                {
                    var updated = UseRequest().entity;
                    var newValue = propertyInfo.GetValue(updated);
                    propertyInfo.SetValue(current, newValue);
                }
            })
            .Then("Save to database", async _ =>
            {
                var cancellationToken = UseCancellationToken();
                await db.SaveChangesAsync(cancellationToken);
            }));
}

