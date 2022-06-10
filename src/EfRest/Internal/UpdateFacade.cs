using CloudCqs;
using CloudCqs.CommandFacade;

namespace EfRest.Internal;

internal class UpdateFacade<TEntity, TKey> : CommandFacade<(string id, string content)>
    where TEntity : class
    where TKey : notnull
{
    public UpdateFacade(CloudCqsOptions option,
        (IQuery<string, TKey> keyDeserializeQuery,
        IQuery<string, TEntity> entityDeserializeQuery,
        ICommand<(TKey id, TEntity entity)> updateCommand) repository)
        : base(option)
    {
        var handler = new Handler()
            .Invoke("Invoke json deserializer to convert id value",
                repository.keyDeserializeQuery,
                p => p.id,
                p => (id: p.response, p.param.content))
            .Invoke("Invoke json deserializer to convert entity",
                repository.entityDeserializeQuery,
                p => p.content,
                p => (p.param.id, entity: p.response))
            .Invoke("Invoke update command",
                repository.updateCommand,
                p => p)
            .Build();

        SetHandler(handler);
    }
}

