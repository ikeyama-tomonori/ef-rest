namespace EfRest.Internal;

using CloudCqs;
using CloudCqs.Facade;

internal class GetListFacade<TEntity>
    : Facade<
        (string? Embed, string? Filter, string? Sort, string? Range),
        (string Json, int Total, (int First, int Last)? Range)
    > where TEntity : class
{
    public GetListFacade(
        CloudCqsOptions option,
        (IQuery<
            (string? Embed, string? Filter, string? Sort, string? Range),
            (TEntity[] Data, int Total, (int First, int Last)? Range)
        > GetListQuery,
        // Serialize to json
        IQuery<TEntity[], string> JsonSerializeQuery) repository
    ) : base(option)
    {
        var handler = new Handler()
            .Invoke(
                "Invoke data query",
                repository.GetListQuery,
                _ => this.UseRequest(),
                p => p.Response
            )
            .Invoke(
                "Invoke data serializer",
                repository.JsonSerializeQuery,
                p => p.Data,
                p => (json: p.Response, p.Param.Total, p.Param.Range)
            );

        this.SetHandler(handler);
    }
}
