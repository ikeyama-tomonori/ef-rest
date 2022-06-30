namespace EfRest.Internal;

using CloudCqs;

internal record RepositoryFactory(
    Func<IFacade<string, string>> CreateFacade,
    Func<
        IFacade<
            (string? Embed, string? Filter, string? Sort, string? Range),
            (string Json, int Total, (int First, int Last)? Range)
        >
    > GetListFacade,
    Func<IFacade<(string Id, string? Embed), string>> GetOneFacade,
    Func<ICommandFacade<(string Id, string Content)>> UpdateFacade,
    Func<ICommandFacade<(string Id, string Content)>> PatchFacade,
    Func<ICommandFacade<string>> DeleteFacade
);
