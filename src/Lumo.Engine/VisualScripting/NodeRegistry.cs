using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Lumo.Engine.VisualScripting;

/// <summary>
/// Declares a <see cref="VSNode"/> subclass as a graph node type. This is the
/// extension API: plugins call <see cref="NodeRegistry.Register{T}"/> or ship
/// attributed types in an assembly passed to <see cref="NodeRegistry.ScanAssembly"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GraphNodeAttribute : Attribute
{
    public string Type { get; }
    public string Title { get; }
    public string Category { get; }
    public string Description { get; }

    public GraphNodeAttribute(string type, string title, string category = "General", string description = "")
    {
        Type = type;
        Title = title;
        Category = category;
        Description = description;
    }
}

/// <summary>
/// Reflection-derived description of a registered node type.
/// </summary>
public sealed class NodeDefinition
{
    public string Type { get; }
    public string Title { get; }
    public string Category { get; }
    public string Description { get; }
    public Type ClrType { get; }

    internal NodeDefinition(GraphNodeAttribute attr, Type clrType)
    {
        Type = attr.Type;
        Title = attr.Title;
        Category = attr.Category;
        Description = attr.Description;
        ClrType = clrType;
    }

    public VSNode Create()
    {
        var node = (VSNode)Activator.CreateInstance(ClrType)!;
        node.TypeId = Type;
        return node;
    }
}

/// <summary>
/// Attribute-based registry of all available node types, including plugin
/// nodes registered at runtime.
/// </summary>
public static class NodeRegistry
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, NodeDefinition> ByType = new(StringComparer.Ordinal);
    private static readonly Dictionary<Type, NodeDefinition> ByClrType = new();

    static NodeRegistry() => ScanAssembly(typeof(VSNode).Assembly);

    public static IReadOnlyList<NodeDefinition> All
    {
        get
        {
            lock (Gate)
            {
                return ByType.Values
                    .OrderBy(d => d.Category, StringComparer.Ordinal)
                    .ThenBy(d => d.Title, StringComparer.Ordinal)
                    .ToList();
            }
        }
    }

    public static void ScanAssembly(Assembly assembly)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t is not null).Cast<Type>().ToArray();
        }

        foreach (Type type in types)
        {
            if (type.IsAbstract || !typeof(VSNode).IsAssignableFrom(type))
                continue;
            if (type.GetCustomAttribute<GraphNodeAttribute>() is GraphNodeAttribute attr)
                Register(attr, type);
        }
    }

    public static void Register<T>() where T : VSNode => RegisterType(typeof(T));

    public static void RegisterType(Type type)
    {
        if (!typeof(VSNode).IsAssignableFrom(type) || type.IsAbstract)
            throw new ArgumentException($"'{type.Name}' is not a concrete VSNode type.", nameof(type));
        GraphNodeAttribute? attr = type.GetCustomAttribute<GraphNodeAttribute>()
            ?? throw new ArgumentException($"'{type.Name}' lacks [GraphNode].", nameof(type));
        Register(attr, type);
    }

    public static bool TryGet(string type, [NotNullWhen(true)] out NodeDefinition? definition)
    {
        lock (Gate)
        {
            return ByType.TryGetValue(type, out definition);
        }
    }

    public static NodeDefinition Get(string type)
    {
        lock (Gate)
        {
            if (!ByType.TryGetValue(type, out NodeDefinition? definition))
                throw new KeyNotFoundException($"Unknown node type '{type}'.");
            return definition;
        }
    }

    public static NodeDefinition GetByClrType(Type type)
    {
        lock (Gate)
        {
            if (ByClrType.TryGetValue(type, out NodeDefinition? definition))
                return definition;
            throw new KeyNotFoundException($"Type '{type.Name}' is not registered.");
        }
    }

    public static VSNode Create(string type) => Get(type).Create();

    private static void Register(GraphNodeAttribute attr, Type type)
    {
        var definition = new NodeDefinition(attr, type);
        lock (Gate)
        {
            ByType[attr.Type] = definition;
            ByClrType[type] = definition;
        }
    }
}
