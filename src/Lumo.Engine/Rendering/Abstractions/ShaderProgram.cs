namespace Lumo.Engine.Rendering.Abstractions;

/// <summary>
/// Shader abstraction wrapping GPU shader programs.
/// </summary>
public sealed class ShaderProgram : IDisposable
{
    public uint Handle { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsCompiled { get; set; }
    private bool _isDisposed;

    public ShaderProgram(string name)
    {
        Name = name;
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;
        _isDisposed = true;
        // GPU cleanup handled by renderer
        GC.SuppressFinalize(this);
    }
}
