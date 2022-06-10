using System;
using System.Text.Json;
using CloudCqs;
using CloudCqs.Query;

namespace EfRest.Internal;

internal class JsonDeserializeQuery<T> : Query<string, T>
    where T : notnull
{
    public JsonDeserializeQuery(CloudCqsOptions option,
        JsonSerializerOptions jsonSerializerOptions,
        Action<JsonException, string> whenError)
        : base(option)
    {
        var handler = new Handler()
            .Then("Deserialize json string", p =>
            {
                var json = p;
                try
                {
                    var data = JsonSerializer.Deserialize<T>(json, jsonSerializerOptions);
                    if (data == null)
                    {
                        throw new NullGuardException(nameof(data));
                    }
                    return data;
                }
                catch (JsonException e)
                {
                    whenError(e, json);
                    throw;
                }
            })
            .Build();

        SetHandler(handler);
    }
}
