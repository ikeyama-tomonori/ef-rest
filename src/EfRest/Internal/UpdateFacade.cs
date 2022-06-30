namespace EfRest.Internal;

using CloudCqs;
using CloudCqs.CommandFacade;

internal class UpdateFacade<TEntity, TKey> : CommandFacade<(string Id, string Content)>
    where TEntity : class
    where TKey : notnull
{
    public UpdateFacade(
        CloudCqsOptions option,
        (IQuery<string, TKey> KeyDeserializeQuery, IQuery<
            string,
            TEntity
        > EntityDeserializeQuery, ICommand<(TKey Id, TEntity Entity)> UpdateCommand) repository
    ) : base(option)
    {
        var handler = new Handler()
            .Invoke(
                "Invoke json deserializer to convert id value",
                repository.KeyDeserializeQuery,
                _ => this.UseRequest().Id,
                p => (id: p.Response, this.UseRequest().Content)
            )
            .Invoke(
                "Invoke json deserializer to convert entity",
                repository.EntityDeserializeQuery,
                p => p.Content,
                p => (p.Param.id, entity: p.Response)
            )
            .Invoke("Invoke update command", repository.UpdateCommand, p => p);

        this.SetHandler(handler);
    }
}
