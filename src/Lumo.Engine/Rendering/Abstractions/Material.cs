using System.Numerics;

namespace Lumo.Engine.Rendering.Abstractions;

/// <summary>
/// Material abstraction for rendering properties.
/// </summary>
public sealed class Material : IDisposable
{
    public string Name { get; set; } = "Default";
    public Vector4 AlbedoColor { get; set; } = new(0.8f, 0.8f, 0.8f, 1.0f);
    public float Metallic { get; set; }
    public float Roughness { get; set; } = 0.5f;
    public float AmbientStrength { get; set; } = 0.1f;
    public string? TexturePath { get; set; }
    public string? ShaderName { get; set; }
    private bool _isDisposed;

    public static Material CreateDefault() => new() { Name = "DefaultMaterial" };

    public static Material CreateColored(Vector4 color) => new()
    {
        Name = "ColoredMaterial",
        AlbedoColor = color
    };

    public void Dispose()
    {
        if (_isDisposed)
            return;
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}
