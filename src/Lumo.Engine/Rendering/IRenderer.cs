using System.Numerics;
using Lumo.Engine.Rendering.Abstractions;

namespace Lumo.Engine.Rendering;

/// <summary>
/// Rendering backend abstraction.
/// </summary>
public interface IRenderer : IDisposable
{
    void Initialize(int width, int height);
    void Resize(int width, int height);
    void BeginFrame(Camera camera);
    void Clear(Vector4 clearColor);
    void DrawMesh(Mesh mesh, Material material, Matrix4x4 transform);
    void DrawGrid(float size, int subdivisions);
    void SetViewProjection(Matrix4x4 view, Matrix4x4 projection, Vector3 cameraPos);
    void SwapBuffers();
    bool IsInitialized { get; }
}

/// <summary>
/// Software fallback renderer.
/// </summary>
public sealed class SoftwareRenderer : IRenderer
{
    private int _width;
    private int _height;

    public bool IsInitialized { get; private set; }

    public void Initialize(int width, int height) { _width = width; _height = height; IsInitialized = true; }
    public void Resize(int width, int height) { _width = width; _height = height; }
    public void BeginFrame(Camera camera) { }
    public void Clear(Vector4 clearColor) { }
    public void DrawMesh(Mesh mesh, Material material, Matrix4x4 transform) { }
    public void DrawGrid(float size, int subdivisions) { }
    public void SetViewProjection(Matrix4x4 view, Matrix4x4 projection, Vector3 cameraPos) { }
    public void SwapBuffers() { }
    public void Dispose() { IsInitialized = false; }
}
