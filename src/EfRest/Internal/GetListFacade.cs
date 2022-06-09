using CloudCqs;
using CloudCqs.Facade;

namespace EfRest.Internal;

internal class GetListFacade<TEntity>
    : Facade<(string? embed, string? filter, string? sort, string? range),
        (string json, int total, (int first, int last)? range)>
    where TEntity : class
{
    public GetListFacade(CloudCqsOptions option,
        (IQuery<(string? embed, string? filter, string? sort, string? range),
            (TEntity[] data, int total, (int first, int last)? range)> getListQuery,
        IQuery<TEntity[], string> jsonSerializeQuery) repository)
        : base(option)
    {
        var handler = new Handler()
            .Invoke("Invoke data query",
                repository.getListQuery,
                p => p,
                p => p.response)
            .Invoke("Invoke data serializer",
            repository.jsonSerializeQuery,
            p => p.data,
            p => (json: p.response, p.param.total, p.param.range))
            .Build();

        SetHandler(handler);
    }
}
