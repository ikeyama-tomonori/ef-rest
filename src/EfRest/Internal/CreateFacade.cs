namespace EfRest.Internal;

using CloudCqs;
using CloudCqs.Facade;

internal class CreateFacade<TEntity, TKey> : Facade<string, string>
    where TEntity : class
    where TKey : notnull
{
    public CreateFacade(
        CloudCqsOptions option,
        // json deserializer to convert data
        (IQuery<string, TEntity> JsonDeserializeQuery,
        // add new record to entity
        INewId<TEntity, TKey> CreateNewId,
        // json serializer to convert id value
        IQuery<TKey, string> JsonSerializeQuery) repository
    ) : base(option)
    {
        var handler = new Handler()
            .Invoke(
                "Invoke json deserializer to convert data",
                repository.JsonDeserializeQuery,
                _ => this.UseRequest(),
                p => p.Response
            )
            .Invoke("Invoke create data", repository.CreateNewId, p => p, p => p.Response)
            .Invoke(
                "Invoke json serializer to convert id value",
                repository.JsonSerializeQuery,
                p => p,
                p => p.Response
            );

        this.SetHandler(handler);
    }
}
