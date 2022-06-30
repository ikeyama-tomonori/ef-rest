namespace EfRest.Internal;

using System.Text.Json;
using CloudCqs;
using CloudCqs.Query;

internal class JsonDeserializeQuery<T> : Query<string, T> where T : notnull
{
    public JsonDeserializeQuery(
        CloudCqsOptions option,
        JsonSerializerOptions jsonSerializerOptions,
        Action<JsonException, string> whenError
    ) : base(option)
    {
        var handler = new Handler().Then(
            "Deserialize json string",
            _ =>
            {
                var json = this.UseRequest();
                try
                {
                    var obj = JsonSerializer.Deserialize<T>(json, jsonSerializerOptions);
                    if (obj is T data)
                    {
                        return data;
                    }
                    throw new TypeGuardException(typeof(T), obj);
                }
                catch (JsonException e)
                {
                    whenError(e, json);
                    throw;
                }
            }
        );

        this.SetHandler(handler);
    }
}
