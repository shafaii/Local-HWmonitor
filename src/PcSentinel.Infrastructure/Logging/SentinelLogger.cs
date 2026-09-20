using Microsoft.Extensions.Logging;

namespace PcSentinel.Infrastructure.Logging;

public static class SentinelLogger
{
    public static ILoggerFactory CreateLoggerFactory()
    {
        return LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
        });
    }
}
