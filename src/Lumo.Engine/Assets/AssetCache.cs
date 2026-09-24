namespace Lumo.Engine.Assets;

/// <summary>
/// Asset metadata for tracking loaded resources.
/// </summary>
public sealed class AssetMetadata
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Path { get; set; } = string.Empty;
    public AssetType Type { get; set; }
    public DateTime LoadedAt { get; set; } = DateTime.UtcNow;
    public long SizeBytes { get; set; }
    public bool IsLoaded { get; set; }
}

public enum AssetType
{
    Unknown,
    Texture,
    Mesh,
    Shader,
    Audio,
    Scene,
    Material,
    Script
}

/// <summary>
/// Asset cache for managing loaded resources.
/// </summary>
public sealed class AssetCache : IDisposable
{
    private readonly Dictionary<string, AssetMetadata> _metadata = [];
    private readonly Dictionary<string, object> _assets = [];

    public T? Load<T>(string path) where T : class
    {
        if (_assets.TryGetValue(path, out var existing))
            return existing as T;

        if (!File.Exists(path))
            return null;

        // Placeholder: real implementation would load based on file type
        var metadata = new AssetMetadata
        {
            Path = path,
            Type = GetAssetType<T>(),
            IsLoaded = true,
            SizeBytes = new FileInfo(path).Length
        };

        _metadata[path] = metadata;

        // Return null for now; real loader would parse the file
        return null;
    }

    public void Unload(string path)
    {
        _assets.Remove(path);
        _metadata.Remove(path);
    }

    public bool IsLoaded(string path) => _metadata.ContainsKey(path) && _metadata[path].IsLoaded;
    public AssetMetadata? GetMetadata(string path) => _metadata.GetValueOrDefault(path);

    public IReadOnlyDictionary<string, AssetMetadata> GetAllMetadata() => _metadata;

    public void Dispose()
    {
        _assets.Clear();
        _metadata.Clear();
    }

    private static AssetType GetAssetType<T>()
    {
        var typeName = typeof(T).Name;
        return typeName switch
        {
            string s when s.Contains("Texture") => AssetType.Texture,
            string s when s.Contains("Mesh") => AssetType.Mesh,
            string s when s.Contains("Shader") => AssetType.Shader,
            _ => AssetType.Unknown
        };
    }
}
