namespace Lumo.Engine.Scripting;

/// <summary>
/// Component that attaches compiled C# scripts to an entity.
/// ScriptNames hold the full class names (e.g. "PlayerController").
/// </summary>
public sealed class ScriptComponent
{
    public List<string> ScriptNames { get; } = [];
    public bool Enabled { get; set; } = true;
}
