namespace EfRest.Internal;

using System.Text.Json;
using CloudCqs;
using CloudCqs.Query;

internal class JsonSerializeQuery<T> : Query<T, string> where T : notnull
{
    public JsonSerializeQuery(CloudCqsOptions option, JsonSerializerOptions jsonSerializerOptions)
        : base(option)
    {
        var handler = new Handler().Then(
            "Serialize to json string",
            _ => JsonSerializer.Serialize(this.UseRequest(), jsonSerializerOptions)
        );
        this.SetHandler(handler);
    }
}
