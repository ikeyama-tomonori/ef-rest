using System;
using CloudCqs;

namespace EfRest.Internal;

internal record RepositoryFactory(
    Func<IFacade<string, string>> CreateFacade,
    Func<IFacade<(string? embed, string? filter, string? sort, string? range),
        (string json, int total, (int first, int last)? range)>> GetListFacade,
    Func<IFacade<(string id, string? embed), string>> GetOneFacade,
    Func<ICommandFacade<(string id, string content)>> UpdateFacade,
    Func<ICommandFacade<(string id, string content)>> PatchFacade,
    Func<ICommandFacade<string>> DeleteFacade
);

