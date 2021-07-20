using System.Linq;
using System.Text.Json;
using CloudCqs;
using CloudCqs.Command;
using Microsoft.EntityFrameworkCore;

namespace EfRest.Internal.EntityHandler
{
    internal class DeleteCommand<TEntity> : Command<string>
        where TEntity : class
    {
        public DeleteCommand(CloudCqsOptions option, DbContext db, JsonSerializerOptions jsonSerializerOptions)
            : base(option)
        {
            var handler = new Handler()
                .Then("Get key's PropertyInfo", props =>
                {
                    var id = props;
                    var propertyInfo = db
                        .Set<TEntity>()
                        .EntityType
                        .FindPrimaryKey()
                        .Properties
                        .Single()
                        .PropertyInfo;
                    return (id, propertyInfo);
                })
                .Then("Get key's value", props =>
                {
                    var (id, propertyInfo) = props;
                    try
                    {
                        var idValue = JsonSerializer.Deserialize(id, propertyInfo.PropertyType, jsonSerializerOptions);
                        if (idValue == null)
                        {
                            throw new NotFoundException(
                                new()
                                {
                                    { "id", new[] { $"Id can not be null: {id}" } }
                                });
                        }
                        return idValue;
                    }
                    catch (JsonException e)
                    {
                        throw new NotFoundException(
                            new()
                            {
                                { "id", new[] { e.Message } }
                            });
                    }
                })
                .Then("Get current entity", async props =>
                {
                    var idValue = props;
                    var entity = await db
                        .Set<TEntity>()
                        .FindAsync(idValue);
                    if (entity == null)
                    {
                        throw new NotFoundException(
                            new()
                            {
                                { "id", new[] { $"Not found: {idValue}" } }
                            });
                    }
                    return entity;
                })
                .Then("Delete entity", props =>
                {
                    var entity = props;

                    db.Set<TEntity>().Remove(entity);
                })
                .Then("Save to database", async () =>
                {
                    await db.SaveChangesAsync();
                })
                .Build();

            SetHandler(handler);
        }
    }
}
