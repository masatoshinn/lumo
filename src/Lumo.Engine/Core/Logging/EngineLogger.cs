using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Lumo.Engine.Core;

/// <summary>
/// Engine logging facade wrapping Microsoft.Extensions.Logging.
/// </summary>
public sealed class EngineLogger
{
    private readonly ILogger<LumoEngine> _logger;

    public EngineLogger(LogLevel minimumLevel = LogLevel.Information)
    {
        using var factory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(minimumLevel);
            builder.AddConsole(options =>
            {
                options.FormatterName = "custom";
            });
            builder.AddSimpleConsole(options =>
            {
                options.TimestampFormat = "[HH:mm:ss.fff] ";
                options.UseUtcTimestamp = false;
            });
        });
        _logger = factory.CreateLogger<LumoEngine>();
    }

    public void LogInformation(string message, params object?[] args)
        => _logger.LogInformation(message, args);

    public void LogWarning(string message, params object?[] args)
        => _logger.LogWarning(message, args);

    public void LogError(string message, params object?[] args)
        => _logger.LogError(message, args);

    public void LogDebug(string message, params object?[] args)
        => _logger.LogDebug(message, args);
}
