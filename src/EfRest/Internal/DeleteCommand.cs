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
                .Then("Get key's PropertyInfo", props =>
                {
                    var (id, cancellationToken) = props;
                    var propertyInfo = db
                        .Set<TEntity>()
                        .EntityType
                        .FindPrimaryKey()
                        .Properties
                        .Single()
                        .PropertyInfo;
                    return (cancellationToken, id, propertyInfo);
                })
                .Then("Get key's value", props =>
                {
                    var (cancellationToken, id, propertyInfo) = props;
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
                .Then("Get current entity", async props =>
                {
                    var (cancellationToken, idValue) = props;
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
                .Then("Delete entity", props =>
                {
                    var (cancellationToken, entity) = props;
                    db.Set<TEntity>().Remove(entity);
                    return cancellationToken;
                })
                .Then("Save to database", async props =>
                {
                    var cancellationToken = props;
                    await db.SaveChangesAsync(cancellationToken);
                })
                .Build();

            SetHandler(handler);
        }
    }
}
