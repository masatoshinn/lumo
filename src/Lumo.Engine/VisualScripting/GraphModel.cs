using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lumo.Engine.Scene;

namespace Lumo.Engine.VisualScripting;

/// <summary>
/// Pin category: exec pins carry control flow, data pins carry typed values.
/// </summary>
public enum PinKind
{
    Exec,
    Data
}

/// <summary>
/// Pin direction relative to the owning node.
/// </summary>
public enum PinDirection
{
    Input,
    Output
}

/// <summary>
/// Static data types used by visual-scripting pins.
/// </summary>
public enum PinDataType
{
    Exec,
    Bool,
    Int,
    Float,
    String,
    Vector3,
    Entity,
    Any
}

/// <summary>
/// Variable storage scope: per-graph local or shared blackboard.
/// </summary>
public enum VariableScope
{
    Local,
    Blackboard
}

/// <summary>
/// A single typed socket on a graph node.
/// </summary>
public sealed class Pin
{
    public string Name { get; init; } = "";
    public PinKind Kind { get; init; }
    public PinDataType DataType { get; init; }
    public PinDirection Direction { get; init; }
}

/// <summary>
/// Directed wire between an output pin and an input pin.
/// </summary>
public sealed class Connection
{
    public string FromNode { get; set; } = "";
    public string FromPin { get; set; } = "";
    public string ToNode { get; set; } = "";
    public string ToPin { get; set; } = "";
}

/// <summary>
/// Declared variable on a graph (local or blackboard scoped).
/// </summary>
public sealed class GraphVariable
{
    public string Name { get; set; } = "";
    public PinDataType DataType { get; set; } = PinDataType.Float;
    public VariableScope Scope { get; set; } = VariableScope.Local;
    public string DefaultValue { get; set; } = "";
}

/// <summary>
/// Conversion helpers between raw string literals and typed pin values.
/// </summary>
public static class PinConvert
{
    private static readonly NumberStyles FloatStyles = NumberStyles.Float;

    public static PinDataType ForType(Type type)
    {
        if (type == typeof(bool)) return PinDataType.Bool;
        if (type == typeof(int)) return PinDataType.Int;
        if (type == typeof(float) || type == typeof(double)) return PinDataType.Float;
        if (type == typeof(string)) return PinDataType.String;
        if (type == typeof(Vector3)) return PinDataType.Vector3;
        if (type == typeof(Entity)) return PinDataType.Entity;
        return PinDataType.Any;
    }

    public static string ToInvariant(object? value) => value switch
    {
        null => "",
        bool b => b ? "true" : "false",
        float f => f.ToString(CultureInfo.InvariantCulture),
        double d => d.ToString(CultureInfo.InvariantCulture),
        int i => i.ToString(CultureInfo.InvariantCulture),
        long l => l.ToString(CultureInfo.InvariantCulture),
        Vector3 v => string.Create(CultureInfo.InvariantCulture, $"{v.X},{v.Y},{v.Z}"),
        _ => value.ToString() ?? ""
    };

    public static object? ToType(string? raw, PinDataType type)
    {
        switch (type)
        {
            case PinDataType.Bool:
                return bool.TryParse(raw, out bool b) ? b : false;
            case PinDataType.Int:
                return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) ? i : 0;
            case PinDataType.Float:
                return float.TryParse(raw, FloatStyles, CultureInfo.InvariantCulture, out float f) ? f : 0f;
            case PinDataType.String:
                return raw ?? "";
            case PinDataType.Vector3:
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    string[] parts = raw.Split(',');
                    if (parts.Length == 3 &&
                        float.TryParse(parts[0].Trim(), FloatStyles, CultureInfo.InvariantCulture, out float x) &&
                        float.TryParse(parts[1].Trim(), FloatStyles, CultureInfo.InvariantCulture, out float y) &&
                        float.TryParse(parts[2].Trim(), FloatStyles, CultureInfo.InvariantCulture, out float z))
                        return new Vector3(x, y, z);
                }
                return Vector3.Zero;
            case PinDataType.Entity:
                return null;
            default:
                return raw ?? "";
        }
    }

    public static T Coerce<T>(object? value)
    {
        if (value is null) return default!;
        if (value is T typed) return typed;
        if (value is string s) return (T?)ToType(s, ForType(typeof(T))) ?? default!;
        try
        {
            return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture)!;
        }
        catch
        {
            return default!;
        }
    }
}

/// <summary>
/// Serializable payload for one node (id, registry type, canvas position, literal values).
/// </summary>
public sealed class NodeData
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public Dictionary<string, string>? Values { get; set; }
}

/// <summary>
/// Serializable payload for one declared graph variable.
/// </summary>
public sealed class VariableData
{
    public string Name { get; set; } = "";
    public PinDataType DataType { get; set; } = PinDataType.Float;
    public VariableScope Scope { get; set; } = VariableScope.Local;
    public string DefaultValue { get; set; } = "";
}

/// <summary>
/// Serializable graph document (nodes, connections, variables).
/// </summary>
public sealed class GraphData
{
    public string Name { get; set; } = "Graph";
    public List<NodeData> Nodes { get; set; } = [];
    public List<Connection> Connections { get; set; } = [];
    public List<VariableData> Variables { get; set; } = [];
}

/// <summary>
/// A visual script graph: nodes, typed connections and declared variables.
/// </summary>
public sealed class VisualGraph
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public string Name { get; set; } = "Graph";
    public List<VSNode> Nodes { get; } = [];
    public List<Connection> Connections { get; } = [];
    public List<GraphVariable> Variables { get; } = [];

    public VSNode? FindNode(string id) => Nodes.FirstOrDefault(n => n.Id == id);

    public Connection? FindInputConnection(string nodeId, string pin) =>
        Connections.FirstOrDefault(c => c.ToNode == nodeId && c.ToPin == pin);

    public IEnumerable<Connection> FindOutputConnections(string nodeId, string pin) =>
        Connections.Where(c => c.FromNode == nodeId && c.FromPin == pin);

    public void AddNode(VSNode node) => Nodes.Add(node);

    public void RemoveNode(string nodeId)
    {
        Nodes.RemoveAll(n => n.Id == nodeId);
        Connections.RemoveAll(c => c.FromNode == nodeId || c.ToNode == nodeId);
    }

    public bool AddConnection(VSNode from, string fromPin, VSNode to, string toPin)
    {
        Pin? outPin = from.GetPin(fromPin);
        Pin? inPin = to.GetPin(toPin);
        if (outPin is null || inPin is null || !GraphValidator.CanConnect(outPin, inPin))
            return false;
        Connections.RemoveAll(c => c.ToNode == to.Id && c.ToPin == toPin);
        Connections.Add(new Connection { FromNode = from.Id, FromPin = fromPin, ToNode = to.Id, ToPin = toPin });
        return true;
    }

    public string ToJson()
    {
        var data = new GraphData { Name = Name };
        foreach (VSNode node in Nodes)
        {
            string typeId = node.TypeId;
            if (typeId.Length == 0)
                typeId = NodeRegistry.GetByClrType(node.GetType()).Type;
            data.Nodes.Add(new NodeData
            {
                Id = node.Id,
                Type = typeId,
                X = node.X,
                Y = node.Y,
                Values = new Dictionary<string, string>(node.Values)
            });
        }
        data.Connections.AddRange(Connections.Select(c => new Connection
        {
            FromNode = c.FromNode,
            FromPin = c.FromPin,
            ToNode = c.ToNode,
            ToPin = c.ToPin
        }));
        data.Variables.AddRange(Variables.Select(v => new VariableData
        {
            Name = v.Name,
            DataType = v.DataType,
            Scope = v.Scope,
            DefaultValue = v.DefaultValue
        }));
        return JsonSerializer.Serialize(data, JsonOptions);
    }

    public static VisualGraph FromJson(string json)
    {
        GraphData? data = JsonSerializer.Deserialize<GraphData>(json, JsonOptions)
            ?? throw new InvalidDataException("Invalid graph JSON.");

        var graph = new VisualGraph { Name = data.Name };
        foreach (NodeData nodeData in data.Nodes)
        {
            if (!NodeRegistry.TryGet(nodeData.Type, out NodeDefinition? definition))
                continue;
            VSNode node = definition.Create();
            node.Id = nodeData.Id.Length > 0 ? nodeData.Id : node.Id;
            node.X = nodeData.X;
            node.Y = nodeData.Y;
            if (nodeData.Values is not null)
            {
                foreach ((string pin, string raw) in nodeData.Values)
                {
                    if (node.GetPin(pin) is Pin p && p.Direction == PinDirection.Input && p.Kind == PinKind.Data)
                        node.Values[pin] = raw;
                }
            }
            graph.Nodes.Add(node);
        }
        foreach (Connection conn in data.Connections)
        {
            if (graph.FindNode(conn.FromNode) is null || graph.FindNode(conn.ToNode) is null)
                continue;
            graph.Connections.Add(conn);
        }
        foreach (VariableData varData in data.Variables)
        {
            graph.Variables.Add(new GraphVariable
            {
                Name = varData.Name,
                DataType = varData.DataType,
                Scope = varData.Scope,
                DefaultValue = varData.DefaultValue
            });
        }
        return graph;
    }

    public void Save(string path) => File.WriteAllText(path, ToJson());

    public static VisualGraph Load(string path) => FromJson(File.ReadAllText(path));
}
