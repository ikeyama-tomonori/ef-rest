using CloudCqs;
using CloudCqs.CommandFacade;

namespace EfRest.Internal;

internal class DeleteFacade<TKey> : CommandFacade<string>
    where TKey : notnull
{
    public DeleteFacade(CloudCqsOptions option,
        (IQuery<string, TKey> jsonDeserializeQuery,
        ICommand<TKey> deleteCommand) repository)
        : base(option)
    {
        var handler = new Handler()
            .Invoke("Invoke json deserializer to convert id value",
                repository.jsonDeserializeQuery,
                p => p,
                p => p.response)
            .Invoke("Invoke delete data",
                repository.deleteCommand,
                p => p)
            .Build();

        SetHandler(handler);
    }
}
