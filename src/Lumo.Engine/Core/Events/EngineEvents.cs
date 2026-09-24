namespace Lumo.Engine.Core;

public static class EngineEvents
{
    public readonly struct Initialized;
    public readonly struct Started;
    public readonly struct Stopped;
    public readonly struct Tick(float deltaTime)
    {
        public float DeltaTime { get; } = deltaTime;
    }
}
