using System;
using CloudCqs;

namespace EfRest.Test;

public static class Options
{
    public static CloudCqsOptions Instance => new()
    {
        RepositoryExecuted = p => Console.WriteLine(
            $"Executed: {p.repositoryType.Name} request={p.request}, response={p.response} in {p.timeSpan.TotalMilliseconds}ms"),
        RepositoryTerminated = p => Console.WriteLine(
            $"Terminated: {p.repositoryType.Name} request={p.request}, exception={p.exception} in {p.timeSpan.TotalMilliseconds}ms"),
        FunctionExecuted = p => Console.WriteLine(
            $"Executed: {p.repositoryType.Name}[{p.description}] request={p.request}, response={p.response} in {p.timeSpan.TotalMilliseconds}ms"),
        FunctionTerminated = p => Console.WriteLine(
            $"Terminated: {p.repositoryType.Name}[{p.description}] request={p.request}, exception={p.exception} in {p.timeSpan.TotalMilliseconds}ms"),
    };
}
