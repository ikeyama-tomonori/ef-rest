using System.Text.Json;
using CloudCqs;
using CloudCqs.Facade;

namespace EfRest.Internal;

internal class GetListFacade<TEntity>
    : Facade<(string? embed, string? filter, string? sort, string? range),
        (string json, int total, (int first, int last)? range)>
    where TEntity : class
{
    public GetListFacade(CloudCqsOptions option,
        IQuery<(string? embed, string? filter, string? sort, string? range),
            (TEntity[] data, int total, (int first, int last)? range)> getListQuery,
        JsonSerializerOptions jsonSerializerOptions) : base(option)
    {
        var handler = new Handler()
            .Invoke($"Invoke {nameof(getListQuery)}",
                getListQuery,
                p => p,
                p => p.response)
            .Then("Serialize data",
            p =>
            {
                var (data, total, range) = p;
                var json = JsonSerializer.Serialize(data, jsonSerializerOptions);
                return (json, total, range);
            })
            .Build();

        SetHandler(handler);
    }
}
