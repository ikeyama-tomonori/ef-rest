namespace EfRest.Test;

using CloudCqs;

public static class Options
{
    public static CloudCqsOptions Instance =>
        new()
        {
            RepositoryExecuted = p =>
                Console.WriteLine(
                    $"Executed: {p.RepositoryType.Name} request={p.Request}, response={p.Response} in {p.TimeSpan.TotalMilliseconds}ms"
                ),
            RepositoryTerminated = p =>
                Console.WriteLine(
                    $"Terminated: {p.RepositoryType.Name} request={p.Request}, exception={p.Exception} in {p.TimeSpan.TotalMilliseconds}ms"
                ),
            FunctionExecuted = p =>
                Console.WriteLine(
                    $"Executed: {p.RepositoryType.Name}[{p.Description}] param={p.Param}, result={p.Result} in {p.TimeSpan.TotalMilliseconds}ms"
                ),
            FunctionTerminated = p =>
                Console.WriteLine(
                    $"Terminated: {p.RepositoryType.Name}[{p.Description}] param={p.Param}, exception={p.Exception} in {p.TimeSpan.TotalMilliseconds}ms"
                ),
        };
}
