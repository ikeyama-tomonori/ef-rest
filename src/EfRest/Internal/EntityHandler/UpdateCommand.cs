using CloudCqs;
using CloudCqs.Command;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Net.Http;
using System.Net.Http.Json;

namespace EfRest.Internal.EntityHandler
{
    internal class UpdateCommand<TEntity> : Command<(string id, HttpContent content)>
        where TEntity : class
    {
        public UpdateCommand(CloudCqsOptions option, DbContext db, JsonSerializerOptions jsonSerializerOptions)
            : base(option)
        {
            var handler = new Handler()
                .Then("Deserialize json", async props =>
                {
                    var (id, content) = props;
                    try
                    {
                        var entity = await content.ReadFromJsonAsync<TEntity>(jsonSerializerOptions);
                        if (entity == null) throw new NullGuardException(nameof(entity));
                        return (id, entity);
                    }
                    catch (JsonException exception)
                    {
                        throw new BadRequestException(new Dictionary<string, string[]>()
                        {
                            { "body", new[]{exception.Message } }
                        });
                    }
                })
                .Then("Validate by annotations", props =>
                {
                    var (id, entity) = props;
                    entity.Validate();
                    return (id, entity);
                })
                .Then("Get key's PropertyInfo", props =>
                {
                    var (id, entity) = props;
                    var propertyInfo = db
                        .Set<TEntity>()
                        .EntityType
                        .FindPrimaryKey()
                        .Properties
                        .Single()
                        .PropertyInfo;
                    return (id, entity, propertyInfo);
                })
                .Then("Set key's value", props =>
                {
                    var (id, entity, propertyInfo) = props;
                    try
                    {
                        var idValue = JsonSerializer.Deserialize(id, propertyInfo.PropertyType, jsonSerializerOptions);
                        propertyInfo.SetValue(entity, idValue);
                        return entity;
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
                .Then("Update entity", props =>
                {
                    var entity = props;

                    db.Set<TEntity>().Update(entity);
                })
                .Then("Save to database", async () =>
                {
                    try
                    {
                        await db.SaveChangesAsync();
                    }
                    catch (DbUpdateConcurrencyException exception)
                    {
                        throw new NotFoundException(
                            new()
                            {
                                { "id", new[] { exception.Message } }
                            });
                    }
                })
                .Build();

            SetHandler(handler);
        }
    }
}
