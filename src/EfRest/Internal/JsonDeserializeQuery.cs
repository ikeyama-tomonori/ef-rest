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
        : base(option) => SetHandler(new Handler()
            .Then("Deserialize json string", _ =>
            {
                var json = UseRequest();
                try
                {
                    var obj = JsonSerializer.Deserialize<T>(json, jsonSerializerOptions);
                    if (obj is T data) return data;
                    throw new TypeGuardException(typeof(T), obj);
                }
                catch (JsonException e)
                {
                    whenError(e, json);
                    throw;
                }
            }));
}
