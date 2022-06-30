namespace EfRest.Internal;

using CloudCqs;
using CloudCqs.Facade;

internal class GetOneFacade<TEntity, TKey> : Facade<(string Id, string? Embed), string>
    where TEntity : notnull
    where TKey : notnull
{
    public GetOneFacade(
        CloudCqsOptions option,
        (IQuery<string, TKey> JsonDeserializeQuery, IQuery<
            (TKey Id, string? Embed),
            TEntity
        > GetOneQuery,
        // Serialize to json
        IQuery<TEntity, string> JsonSerializeQuery) repository
    ) : base(option)
    {
        var handler = new Handler()
            .Invoke(
                "Invoke json deserializer to convert id value",
                repository.JsonDeserializeQuery,
                _ => this.UseRequest().Id,
                p => (id: p.Response, this.UseRequest().Embed)
            )
            .Invoke($"Invoke data query", repository.GetOneQuery, p => p, p => p.Response)
            .Invoke(
                "Invoke json serializer to convert response data",
                repository.JsonSerializeQuery,
                p => p,
                p => p.Response
            );

        this.SetHandler(handler);
    }
}
