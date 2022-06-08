using System.Text.Json;
using CloudCqs;
using CloudCqs.Facade;

namespace EfRest.Internal;

internal class CreateFacade<TEntity, TKey> : Facade<string, string>
    where TEntity : class
    where TKey : notnull
{
    public CreateFacade(CloudCqsOptions option,
        INewId<TEntity, TKey> createNewId,
        JsonSerializerOptions jsonSerializerOptions) : base(option)
    {
        var handler = new Handler()
            .Then("Deserialize json", p =>
            {
                var content = p;
                try
                {
                    var entity = JsonSerializer.Deserialize<TEntity>(
                        content,
                        jsonSerializerOptions);
                    if (entity == null) throw new NullGuardException(nameof(entity));
                    return entity;
                }
                catch (JsonException exception)
                {
                    throw new BadRequestException(new()
                    {
                        ["body"] = new[] { exception.Message }
                    });
                }
            })
            .Invoke($"Invoke {createNewId}",
                createNewId,
                p => p,
                p => p.response)
            .Then("Serialize id value", p =>
            {
                var value = p;
                var id = JsonSerializer.Serialize(value, jsonSerializerOptions);
                return id;
            })
            .Build();
        SetHandler(handler);
    }
}

