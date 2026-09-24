namespace Lumo.Engine.Core;

/// <summary>
/// Configuration for engine initialization.
/// </summary>
public sealed class EngineConfiguration
{
    public string Name { get; set; } = "Lumo Engine";
    public int TargetFps { get; set; } = 60;
    public bool UseFixedTimestep { get; set; } = true;
    public float FixedTimestep { get; set; } = 1.0f / 60.0f;
    public Microsoft.Extensions.Logging.LogLevel LogLevel { get; set; } = Microsoft.Extensions.Logging.LogLevel.Information;
}
