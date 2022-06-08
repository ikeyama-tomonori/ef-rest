using System.Text.Json;
using CloudCqs;
using CloudCqs.Facade;

namespace EfRest.Internal;

internal class GetOneFacade<TEntity, TKey> : Facade<(string id, string? embed), string>
        where TEntity : class
{
    public GetOneFacade(CloudCqsOptions option,
        IQuery<(TKey id, string? embed), TEntity> getOneQuery,
        JsonSerializerOptions jsonSerializerOptions) : base(option)
    {
        var handler = new Handler()
            .Then("Convert id to key's value", p =>
            {
                var (id, embed) = p;
                try
                {
                    var value = JsonSerializer.Deserialize<TKey>(id, jsonSerializerOptions);
                    if (value == null)
                    {
                        throw new NullGuardException(nameof(value));
                    }
                    return (value, embed);
                }
                catch (JsonException e)
                {
                    throw new NotFoundException(new()
                    {
                        ["id"] = new[] { e.Message }
                    });
                }
            })
            .Invoke($"Invoke {nameof(getOneQuery)}",
                getOneQuery,
                p => p,
                p => p.response)
            .Then("Serialize data",
                p =>
                {
                    var data = p;
                    var json = JsonSerializer.Serialize(data, jsonSerializerOptions);
                    return json;
                })
            .Build();

        SetHandler(handler);
    }
}
