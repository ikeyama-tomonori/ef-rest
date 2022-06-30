namespace EfRest;

using System.Net;
using CloudCqs;
using CloudCqs.Command;
using Microsoft.EntityFrameworkCore;

public class UpdateCommand<TEntity, TKey> : Command<(TKey Id, TEntity Entity)> where TEntity : class
{
    public UpdateCommand(CloudCqsOptions option, DbContext db) : base(option)
    {
        var handler = new Handler()
            .Then(
                "Get current entity",
                async p =>
                {
                    var (idValue, _) = this.UseRequest();
                    var cancellationToken = this.UseCancellationToken();
                    var current = await db.Set<TEntity>()
                        .FindAsync(new[] { idValue as object }, cancellationToken);
                    if (current == null)
                    {
                        throw new StatusCodeException(
                            HttpStatusCode.NotFound,
                            new($"Not found: {idValue}", new[] { "id" })
                        );
                    }
                    return current;
                }
            )
            .Then(
                "Get key's prop name",
                p =>
                {
                    var current = p;
                    var keyName = db.Set<TEntity>()
                        .EntityType.FindPrimaryKey()
                        ?.Properties.SingleOrDefault()
                        ?.PropertyInfo?.Name;
                    if (keyName == null)
                    {
                        throw new StatusCodeException(
                            HttpStatusCode.BadRequest,
                            new($"Entity must have single primary key.", new[] { "resource" })
                        );
                    }
                    return (current, keyName);
                }
            )
            .Then(
                "Update current entity",
                p =>
                {
                    var (current, keyPropertyName) = p;
                    var propertyInfos = typeof(TEntity)
                        .GetProperties()
                        .Where(p => p.Name != keyPropertyName);
                    foreach (var propertyInfo in propertyInfos)
                    {
                        var (_, updated) = this.UseRequest();
                        var newValue = propertyInfo.GetValue(updated);
                        propertyInfo.SetMethod?.Invoke(current, new[] { newValue });
                    }
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
