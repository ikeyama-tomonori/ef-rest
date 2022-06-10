using CloudCqs;
using CloudCqs.Facade;

namespace EfRest.Internal;

internal class GetOneFacade<TEntity, TKey> : Facade<(string id, string? embed), string>
    where TEntity : notnull
    where TKey : notnull
{
    public GetOneFacade(CloudCqsOptions option,
        (IQuery<string, TKey> jsonDeserializeQuery,
        IQuery<(TKey id, string? embed), TEntity> getOneQuery,
        IQuery<TEntity, string> jsonSerializeQuery) repository) : base(option)
    {
        var handler = new Handler()
            .Invoke("Invoke json deserializer to convert id value",
                repository.jsonDeserializeQuery,
                p => p.id,
                p => (id: p.response, p.param.embed))
            .Invoke($"Invoke data query",
                repository.getOneQuery,
                p => p,
                p => p.response)
            .Invoke("Invoke json serializer to convert response data",
                repository.jsonSerializeQuery,
                p => p,
                p => p.response)
            .Build();

        SetHandler(handler);
    }
}
