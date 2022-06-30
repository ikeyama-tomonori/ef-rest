namespace EfRest;

using System.Net;
using CloudCqs;
using CloudCqs.Command;
using Microsoft.EntityFrameworkCore;

public class DeleteCommand<TEntity, TKey> : Command<TKey>
    where TEntity : class
    where TKey : notnull
{
    public DeleteCommand(CloudCqsOptions option, DbContext db) : base(option)
    {
        var handler = new Handler()
            .Then(
                "Get current entity",
                async _ =>
                {
                    var idValue = this.UseRequest();
                    var cancellationToken = this.UseCancellationToken();
                    var entity = await db.Set<TEntity>()
                        .FindAsync(new[] { idValue as object }, cancellationToken);
                    if (entity == null)
                    {
                        throw new StatusCodeException(
                            HttpStatusCode.NotFound,
                            new($"Not found: {idValue}", new[] { "id" })
                        );
                    }
                    return entity;
                }
            )
            .Then(
                "Delete entity",
                p =>
                {
                    var entity = p;
                    db.Set<TEntity>().Remove(entity);
                }
            )
            .Then(
                "Save to database",
                async _ =>
                {
                    var cancellationToken = this.UseCancellationToken();
                    await db.SaveChangesAsync(cancellationToken);
                }
            );

        this.SetHandler(handler);
    }
}
