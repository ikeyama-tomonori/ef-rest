using System.Text.Json;
using CloudCqs;
using CloudCqs.Query;

namespace EfRest.Internal;

internal class JsonSerializeQuery<T> : Query<T, string>
    where T : notnull
{
    public JsonSerializeQuery(CloudCqsOptions option, JsonSerializerOptions jsonSerializerOptions)
        : base(option) => SetHandler(new Handler()
            .Then("Serialize to json string", _ =>
            JsonSerializer.Serialize(UseRequest(), jsonSerializerOptions)));

}
