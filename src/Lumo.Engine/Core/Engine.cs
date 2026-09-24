using System.Numerics;

namespace Lumo.Engine.Core;

/// <summary>
/// Central engine class that manages the game loop, timing, and subsystem lifecycle.
/// </summary>
public sealed class LumoEngine : IDisposable
{
    private readonly EngineConfiguration _config;
    private readonly TimeManager _time;
    private readonly EventBus _events;
    private readonly EngineLogger _logger;
    private bool _isRunning;
    private bool _isDisposed;

    public TimeManager Time => _time;
    public EventBus Events => _events;
    public EngineLogger Logger => _logger;
    public bool IsRunning => _isRunning;

    public LumoEngine(EngineConfiguration? config = null)
    {
        _config = config ?? new EngineConfiguration();
        _time = new TimeManager();
        _events = new EventBus();
        _logger = new EngineLogger(_config.LogLevel);
    }

    /// <summary>
    /// Initialize the engine subsystems.
    /// </summary>
    public void Initialize()
    {
        _logger.LogInformation("Lumo Engine v{Version} initializing...", EngineConstants.Version);
        _events.Raise(new EngineEvents.Initialized());
    }

    /// <summary>
    /// Run a single frame of the engine loop.
    /// Returns the delta time for this frame.
    /// </summary>
    public float Tick()
    {
        _time.Update();

        float dt = _time.DeltaTime;
        _events.Raise(new EngineEvents.Tick(dt));

        return dt;
    }

    /// <summary>
    /// Start the engine loop.
    /// </summary>
    public void Start()
    {
        if (_isRunning)
            return;

        _isRunning = true;
        _logger.LogInformation("Engine started.");
        _events.Raise(new EngineEvents.Started());
    }

    /// <summary>
    /// Stop the engine loop.
    /// </summary>
    public void Stop()
    {
        if (!_isRunning)
            return;

        _isRunning = false;
        _logger.LogInformation("Engine stopped.");
        _events.Raise(new EngineEvents.Stopped());
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        Stop();
        _events.Dispose();
        _logger.LogInformation("Engine disposed.");
    }
}
