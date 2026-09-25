using Lumo.Engine.Rendering.Abstractions;

namespace Lumo.Engine.Assets;

/// <summary>
/// Global registry of meshes by name. Built-in primitives are pre-registered;
/// imported meshes (OBJ) are added on import.
/// </summary>
public static class MeshLibrary
{
    private static readonly Dictionary<string, Mesh> _meshes = new(StringComparer.OrdinalIgnoreCase);

    static MeshLibrary()
    {
        Register(Mesh.CreateCube());
        Register(Mesh.CreateQuad());
        Register(Mesh.CreateTriangle());
    }

    public static void Register(Mesh mesh) => _meshes[mesh.Name] = mesh;

    public static Mesh? Get(string? name)
        => name != null && _meshes.TryGetValue(name, out var m) ? m : null;

    public static bool Contains(string name) => _meshes.ContainsKey(name);

    public static IReadOnlyCollection<string> Names => _meshes.Keys;
}
