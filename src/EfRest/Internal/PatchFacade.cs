using System.Text.Json;
using CloudCqs;
using CloudCqs.CommandFacade;

namespace EfRest.Internal;

internal class PatchFacade<TKey> : CommandFacade<(string id, string content)>
    where TKey : notnull
{
    public PatchFacade(CloudCqsOptions option,
        (IQuery<string, TKey> keyDeserializeQuery,
        IQuery<string, JsonElement> patchDeserializeQuery,
        ICommand<(TKey id, JsonElement patch)> patchCommand) repository)
        : base(option)
    {
        var handler = new Handler()
            .Invoke("Invoke json deserializer to convert id value",
                repository.keyDeserializeQuery,
                p => p.id,
                p => (id: p.response, p.param.content))
            .Invoke("Invoke json deserializer to convert entity",
                repository.patchDeserializeQuery,
                p => p.content,
                p => (p.param.id, entity: p.response))
            .Invoke("Invoke update command",
                repository.patchCommand,
                p => p)
            .Build();

        SetHandler(handler);
    }
}

