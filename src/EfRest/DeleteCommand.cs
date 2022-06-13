using CloudCqs;
using CloudCqs.Command;
using Microsoft.EntityFrameworkCore;

namespace EfRest;

public class DeleteCommand<TEntity, TKey> : Command<TKey>
    where TEntity : class
    where TKey : notnull
{
    public DeleteCommand(CloudCqsOptions option, DbContext db)
        : base(option) => SetHandler(new Handler()
            .Then("Get current entity", async _ =>
            {
                var idValue = UseRequest();
                var cancellationToken = UseCancellationToken();
                var entity = await db
                    .Set<TEntity>()
                    .FindAsync(new[] { idValue as object }, cancellationToken);
                if (entity == null)
                {
                    throw new NotFoundException(new()
                    {
                        ["id"] = new[] { $"Not found: {idValue}" }
                    });
                }
                return entity;
            })
            .Then("Delete entity", p =>
            {
                var entity = p;
                db.Set<TEntity>().Remove(entity);
            })
            .Then("Save to database", async _ =>
            {
                var cancellationToken = UseCancellationToken();
                await db.SaveChangesAsync(cancellationToken);
            }));
}

