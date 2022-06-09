using CloudCqs;
using CloudCqs.Facade;

namespace EfRest.Internal;

internal class CreateFacade<TEntity, TKey> : Facade<string, string>
    where TEntity : class
    where TKey : notnull
{
    public CreateFacade(CloudCqsOptions option,
        (IQuery<string, TEntity> jsonDeserializeQuery,
        INewId<TEntity, TKey> createNewId,
        IQuery<TKey, string> jsonSerializeQuery) repository)
        : base(option)
    {
        var handler = new Handler()
            .Invoke("Invoke json deserializer to convert data",
                repository.jsonDeserializeQuery,
                p => p,
                p => p.response)
            .Invoke("Invoke create data",
                repository.createNewId,
                p => p,
                p => p.response)
            .Invoke("Invoke json serializer to convert id value",
                repository.jsonSerializeQuery,
                p => p,
                p => p.response)
            .Build();

        SetHandler(handler);
    }
}

