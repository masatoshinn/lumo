using System.Numerics;
using Lumo.Engine.Input;
using Entity = Lumo.Engine.Scene.Entity;
using GameScene = Lumo.Engine.Scene.Scene;

namespace Lumo.Engine.Scripting;

/// <summary>
/// Base class for all C# game scripts. User scripts inherit this
/// and override lifecycle callbacks.
/// </summary>
public abstract class LumoScript
{
    /// <summary>The entity this script is attached to.</summary>
    public Entity Entity { get; internal set; } = null!;

    /// <summary>The scene containing the attached entity.</summary>
    public GameScene? Scene => Entity?.ParentScene;

    /// <summary>Delta time of the current frame (seconds).</summary>
    public float DeltaTime { get; internal set; }

    /// <summary>Total elapsed time since play started (seconds).</summary>
    public float Time { get; internal set; }

    /// <summary>Input state (available during play mode).</summary>
    public InputState? Input { get; internal set; }

    /// <summary>Called once when play mode starts.</summary>
    public virtual void OnStart() { }

    /// <summary>Called every frame while play mode runs.</summary>
    public virtual void OnUpdate(float deltaTime) { }

    /// <summary>Called once when play mode stops.</summary>
    public virtual void OnDestroy() { }

    // ---- Convenience helpers ----

    protected Vector3 Position
    {
        get => Entity.Transform.Position;
        set => Entity.Transform.Position = value;
    }

    protected Vector3 Scale
    {
        get => Entity.Transform.Scale;
        set => Entity.Transform.Scale = value;
    }

    protected void Log(string message) => ScriptHost.Log(message);

    protected Entity? Find(string name) => Scene?.FindByName(name);

    protected void Translate(Vector3 delta) => Entity.Transform.Position += delta;

    protected void RotateEuler(float pitch, float yaw, float roll)
        => Entity.Transform.SetRotationFromEuler(
            Entity.Transform.GetEulerAngles().X + pitch,
            Entity.Transform.GetEulerAngles().Y + yaw,
            Entity.Transform.GetEulerAngles().Z + roll);
}
