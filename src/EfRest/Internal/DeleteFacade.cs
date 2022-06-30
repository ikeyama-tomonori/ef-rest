namespace EfRest.Internal;

using CloudCqs;
using CloudCqs.CommandFacade;

internal class DeleteFacade<TKey> : CommandFacade<string> where TKey : notnull
{
    public DeleteFacade(
        CloudCqsOptions option,
        (IQuery<string, TKey> JsonDeserializeQuery, ICommand<TKey> DeleteCommand) repository
    ) : base(option)
    {
        var handler = new Handler()
            .Invoke(
                "Invoke json deserializer to convert id value",
                repository.JsonDeserializeQuery,
                _ => this.UseRequest(),
                p => p.Response
            )
            .Invoke("Invoke delete data", repository.DeleteCommand, p => p);

        this.SetHandler(handler);
    }
}
