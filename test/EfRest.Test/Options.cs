using CloudCqs;
using Microsoft.Extensions.Logging;

namespace EfRest.Test
{
    public static class Options
    {
        private static ILoggerFactory Logger => LoggerFactory.Create(configure => configure.AddConsole());
        public static CloudCqsOptions Instance => new(Logger);
    }
}
