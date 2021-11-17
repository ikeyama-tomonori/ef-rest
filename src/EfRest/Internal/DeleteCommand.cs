using System.Linq;
using System.Text.Json;
using System.Threading;
using CloudCqs;
using CloudCqs.Command;
using Microsoft.EntityFrameworkCore;

namespace EfRest.Internal
{
    internal class DeleteCommand<TEntity> : Command<(string id, CancellationToken cancellationToken)>
        where TEntity : class
    {
        public DeleteCommand(CloudCqsOptions option, DbContext db, JsonSerializerOptions jsonSerializerOptions)
            : base(option)
        {
            var handler = new Handler()
                .Then("Get key's PropertyInfo", p =>
                {
                    var (id, cancellationToken) = p;
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
                    return (cancellationToken, id, propertyInfo);
                })
                .Then("Get key's value", p =>
                {
                    var (cancellationToken, id, propertyInfo) = p;
                    try
                    {
                        var idValue = JsonSerializer.Deserialize(id, propertyInfo.PropertyType, jsonSerializerOptions);
                        if (idValue == null)
                        {
                            throw new NotFoundException(new()
                            {
                                ["id"] = new[] { $"Id can not be null: {id}" }
                            });
                        }
                        return (cancellationToken, idValue);
                    }
                    catch (JsonException e)
                    {
                        throw new NotFoundException(new()
                        {
                            ["id"] = new[] { e.Message }
                        });
                    }
                })
                .Then("Get current entity", async p =>
                {
                    var (cancellationToken, idValue) = p;
                    var entity = await db
                        .Set<TEntity>()
                        .FindAsync(new[] { idValue }, cancellationToken);
                    if (entity == null)
                    {
                        throw new NotFoundException(new()
                        {
                            ["id"] = new[] { $"Not found: {idValue}" }
                        });
                    }
                    return (cancellationToken, entity);
                })
                .Then("Delete entity", p =>
                {
                    var (cancellationToken, entity) = p;
                    db.Set<TEntity>().Remove(entity);
                    return cancellationToken;
                })
                .Then("Save to database", async p =>
                {
                    var cancellationToken = p;
                    await db.SaveChangesAsync(cancellationToken);
                })
                .Build();

            SetHandler(handler);
        }
    }
}
