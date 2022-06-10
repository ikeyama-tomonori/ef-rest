using System.Threading;
using CloudCqs;
using CloudCqs.Command;
using Microsoft.EntityFrameworkCore;

namespace EfRest;

public class DeleteCommand<TEntity, TKey> : Command<TKey>
    where TEntity : class
    where TKey : notnull
{
    public DeleteCommand(CloudCqsOptions option, DbContext db, CancellationToken cancellationToken)
        : base(option)
    {
        var handler = new Handler()
            .Then("Get current entity", async p =>
            {
                var idValue = p;
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
                await db.SaveChangesAsync(cancellationToken);
            })
            .Build();

        SetHandler(handler);
    }
}
