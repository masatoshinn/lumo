using System.Numerics;

namespace Lumo.Engine.Rendering.Abstractions;

/// <summary>
/// Light types supported by the engine.
/// </summary>
public enum LightType
{
    Directional,
    Point,
    Spot
}

/// <summary>
/// Light abstraction for scene lighting.
/// </summary>
public sealed class Light
{
    public LightType Type { get; set; } = LightType.Directional;
    public Vector3 Position { get; set; } = Vector3.Zero;
    public Vector3 Direction { get; set; } = -Vector3.UnitY;
    public Vector3 Color { get; set; } = Vector3.One;
    public float Intensity { get; set; } = 1.0f;
    public float Range { get; set; } = 50.0f;
    public float InnerCutoff { get; set; } = 12.5f;
    public float OuterCutoff { get; set; } = 17.5f;
}
