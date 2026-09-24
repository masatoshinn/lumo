using System.Diagnostics;

namespace Lumo.Engine.Core;

/// <summary>
/// Manages engine time including delta time, elapsed time, and frame counting.
/// </summary>
public sealed class TimeManager
{
    private readonly Stopwatch _stopwatch;
    private long _lastTimestamp;
    private float _fixedAccumulator;

    public float DeltaTime { get; private set; }
    public double ElapsedTime => _stopwatch.Elapsed.TotalSeconds;
    public ulong FrameCount { get; private set; }
    public float TimeScale { get; set; } = 1.0f;
    public float FPS { get; private set; }

    private readonly Stopwatch _fpsStopwatch;
    private int _fpsFrameCount;
    private float _fpsAccumulator;

    public TimeManager()
    {
        _stopwatch = new Stopwatch();
        _fpsStopwatch = new Stopwatch();
        _stopwatch.Start();
        _fpsStopwatch.Start();
        _lastTimestamp = _stopwatch.ElapsedTicks;
    }

    public void Update()
    {
        long currentTimestamp = _stopwatch.ElapsedTicks;
        long elapsed = currentTimestamp - _lastTimestamp;
        _lastTimestamp = currentTimestamp;

        DeltaTime = (float)TimeSpan.FromTicks(elapsed).TotalSeconds * TimeScale;
        FrameCount++;

        _fpsFrameCount++;
        _fpsAccumulator += DeltaTime;

        if (_fpsAccumulator >= 1.0f)
        {
            FPS = _fpsFrameCount / _fpsAccumulator;
            _fpsFrameCount = 0;
            _fpsAccumulator = 0;
        }
    }

    /// <summary>
    /// Consume fixed timestep ticks for physics/game logic.
    /// Returns the number of fixed steps to execute.
    /// </summary>
    public int ConsumeFixedTimestep(float fixedDelta)
    {
        _fixedAccumulator += DeltaTime;
        int steps = 0;

        while (_fixedAccumulator >= fixedDelta)
        {
            steps++;
            _fixedAccumulator -= fixedDelta;
        }

        return steps;
    }

    public void Reset()
    {
        _stopwatch.Restart();
        _lastTimestamp = 0;
        DeltaTime = 0;
        FrameCount = 0;
        _fixedAccumulator = 0;
    }
}
