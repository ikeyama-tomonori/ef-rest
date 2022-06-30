namespace EfRest.Internal;

using System.Text.Json;
using CloudCqs;
using CloudCqs.CommandFacade;

internal class PatchFacade<TKey> : CommandFacade<(string Id, string Content)> where TKey : notnull
{
    public PatchFacade(
        CloudCqsOptions option,
        (IQuery<string, TKey> KeyDeserializeQuery, IQuery<
            string,
            JsonElement
        > PatchDeserializeQuery, ICommand<(TKey Id, JsonElement Patch)> PatchCommand) repository
    ) : base(option)
    {
        var handler = new Handler()
            .Invoke(
                "Invoke json deserializer to convert id value",
                repository.KeyDeserializeQuery,
                p => this.UseRequest().Id,
                p => (id: p.Response, this.UseRequest().Content)
            )
            .Invoke(
                "Invoke json deserializer to convert entity",
                repository.PatchDeserializeQuery,
                p => p.Content,
                p => (p.Param.id, entity: p.Response)
            )
            .Invoke("Invoke update command", repository.PatchCommand, p => p);

        this.SetHandler(handler);
    }
}
