using System;
using System.Collections.Specialized;
using System.Net.Http;
using System.Threading;
using CloudCqs;

namespace EfRest.Internal;

internal record RepositoryFactory(
    Func<CancellationToken, INewId<string, string>> CreateNewId,
    Func<CancellationToken, ICommand<(string id, string content)>> UpdateCommand,
    Func<CancellationToken, IQuery<(string id, NameValueCollection param), HttpResponseMessage>> GetOneQuery,
    Func<CancellationToken, ICommand<string>> DeleteCommand,
    Func<CancellationToken, IFacade<(string? embed, string? filter, string? sort, string? range),
        (string json, int total, (int first, int last)? range)>> GetListFacade
);

