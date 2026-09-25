namespace Lumo.Engine.Scene;

/// <summary>
/// Represents a game object in the scene with components.
/// </summary>
public sealed class Entity
{
    private static long _nextId;

    public long Id { get; } = Interlocked.Increment(ref _nextId);
    public string Name { get; set; } = "Entity";
    public bool IsActive { get; set; } = true;
    public Scene? ParentScene { get; internal set; }
    public Entity? Parent { get; internal set; }
    public List<Entity> Children { get; } = [];

    public TransformComponent Transform { get; } = new();
    public MeshRendererComponent? MeshRenderer { get; set; }
    public SpriteRendererComponent? SpriteRenderer { get; set; }
    public CameraComponent? Camera { get; set; }
    public LightComponent? Light { get; set; }
    public Lumo.Engine.Scripting.ScriptComponent? Scripts { get; set; }

    public Entity() { }

    public Entity(string name)
    {
        Name = name;
    }

    public Entity AddChild(string name = "Child")
    {
        var child = new Entity(name)
        {
            ParentScene = ParentScene,
            Parent = this
        };
        Children.Add(child);
        ParentScene?.OnEntityAdded(child);
        return child;
    }

    public void RemoveChild(Entity child)
    {
        child.Parent = null;
        Children.Remove(child);
    }

    public T? GetComponent<T>() where T : class
    {
        return typeof(T) switch
        {
            Type t when t == typeof(TransformComponent) => Transform as T,
            Type t when t == typeof(MeshRendererComponent) => MeshRenderer as T,
            Type t when t == typeof(SpriteRendererComponent) => SpriteRenderer as T,
            Type t when t == typeof(CameraComponent) => Camera as T,
            Type t when t == typeof(LightComponent) => Light as T,
            Type t when t == typeof(Lumo.Engine.Scripting.ScriptComponent) => Scripts as T,
            _ => null
        };
    }

    public T AddComponent<T>() where T : class, new()
    {
        var component = new T();
        SetComponent(component);
        return component;
    }

    private void SetComponent<T>(T component) where T : class
    {
        switch (component)
        {
            case MeshRendererComponent c: MeshRenderer = c; break;
            case SpriteRendererComponent c: SpriteRenderer = c; break;
            case CameraComponent c: Camera = c; break;
            case LightComponent c: Light = c; break;
            case Lumo.Engine.Scripting.ScriptComponent c: Scripts = c; break;
        }
    }

    public bool HasComponent<T>() where T : class
    {
        return typeof(T) switch
        {
            Type t when t == typeof(TransformComponent) => true,
            Type t when t == typeof(MeshRendererComponent) => MeshRenderer != null,
            Type t when t == typeof(SpriteRendererComponent) => SpriteRenderer != null,
            Type t when t == typeof(CameraComponent) => Camera != null,
            Type t when t == typeof(LightComponent) => Light != null,
            Type t when t == typeof(Lumo.Engine.Scripting.ScriptComponent) => Scripts != null,
            _ => false
        };
    }

    public override string ToString() => $"Entity '{Name}' (ID: {Id})";
}
