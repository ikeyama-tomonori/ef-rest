using System.Text.Json;
using CloudCqs;
using CloudCqs.Query;

namespace EfRest.Internal;

internal class JsonDeserializeQuery<T> : Query<string, T>
    where T : notnull
{
    public JsonDeserializeQuery(CloudCqsOptions option, JsonSerializerOptions jsonSerializerOptions) : base(option)
    {
        var handler = new Handler()
            .Then("Deserialize json string", p =>
            {
                try
                {
                    var data = JsonSerializer.Deserialize<T>(p, jsonSerializerOptions);
                    if (data == null)
                    {
                        throw new NullGuardException(nameof(data));
                    }
                    return data;
                }
                catch (JsonException e)
                {
                    throw new NotFoundException(new()
                    {
                        ["id"] = new[] { e.Message }
                    });
                }
            })
            .Build();

        SetHandler(handler);
    }
}
