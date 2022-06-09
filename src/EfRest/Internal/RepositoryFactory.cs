using System;
using System.Threading;
using CloudCqs;

namespace EfRest.Internal;

internal record RepositoryFactory(
    Func<CancellationToken, IFacade<string, string>> CreateFacade,
    Func<CancellationToken, ICommand<(string id, string content)>> UpdateCommand,
    Func<CancellationToken, IFacade<(string id, string? embed), string>> GetOneFacade,
    Func<CancellationToken, ICommandFacade<string>> DeleteFacade,
    Func<CancellationToken, IFacade<(string? embed, string? filter, string? sort, string? range),
        (string json, int total, (int first, int last)? range)>> GetListFacade
);

