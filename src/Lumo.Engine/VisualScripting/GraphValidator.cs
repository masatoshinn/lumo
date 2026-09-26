namespace Lumo.Engine.VisualScripting;

/// <summary>
/// A single graph validation finding.
/// </summary>
public sealed class ValidationIssue
{
    public string? NodeId { get; init; }
    public string Message { get; init; } = "";
    public bool IsError { get; init; } = true;

    public override string ToString() => $"{(IsError ? "Error" : "Warning")}: {Message}";
}

/// <summary>
/// Static graph checks: pin type compatibility, endpoint existence,
/// duplicate exec inputs and pure-data cycle detection.
/// </summary>
public static class GraphValidator
{
    /// <summary>True when a wire from <paramref name="from"/> to <paramref name="to"/> is legal.</summary>
    public static bool CanConnect(Pin? from, Pin? to)
    {
        if (from is null || to is null)
            return false;
        if (from.Direction != PinDirection.Output || to.Direction != PinDirection.Input)
            return false;
        if (from.Kind != to.Kind)
            return false;
        if (from.Kind == PinKind.Exec)
            return true;
        return Compatible(from.DataType, to.DataType);
    }

    /// <summary>Implicit conversions: int widens to float, Any accepts everything.</summary>
    public static bool Compatible(PinDataType from, PinDataType to) =>
        from == to || from == PinDataType.Any || to == PinDataType.Any ||
        (from == PinDataType.Int && to == PinDataType.Float);

    public static List<ValidationIssue> Validate(VisualGraph graph)
    {
        var issues = new List<ValidationIssue>();
        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (VSNode node in graph.Nodes)
        {
            if (!ids.Add(node.Id))
                issues.Add(new ValidationIssue { NodeId = node.Id, Message = $"Duplicate node id '{node.Id}'." });
            if (node.TypeId.Length > 0 && !NodeRegistry.TryGet(node.TypeId, out _))
                issues.Add(new ValidationIssue { NodeId = node.Id, Message = $"Unknown node type '{node.TypeId}'." });
        }

        var dataInputs = new Dictionary<(string Node, string Pin), int>();

        foreach (Connection conn in graph.Connections)
        {
            VSNode? from = graph.FindNode(conn.FromNode);
            VSNode? to = graph.FindNode(conn.ToNode);
            if (from is null)
            {
                issues.Add(new ValidationIssue { Message = $"Connection references missing source node '{conn.FromNode}'." });
                continue;
            }
            if (to is null)
            {
                issues.Add(new ValidationIssue { Message = $"Connection references missing target node '{conn.ToNode}'." });
                continue;
            }

            Pin? fromPin = from.GetPin(conn.FromPin);
            Pin? toPin = to.GetPin(conn.ToPin);
            if (fromPin is null || toPin is null)
            {
                issues.Add(new ValidationIssue
                {
                    NodeId = to.Id,
                    Message = $"Connection references missing pin '{(fromPin is null ? conn.FromNode + "." + conn.FromPin : conn.ToNode + "." + conn.ToPin)}'."
                });
                continue;
            }

            if (!CanConnect(fromPin, toPin))
            {
                issues.Add(new ValidationIssue
                {
                    NodeId = to.Id,
                    Message = $"Incompatible pins: {from.TypeId}.{fromPin.Name} ({fromPin.DataType}) -> {to.TypeId}.{toPin.Name} ({toPin.DataType})."
                });
                continue;
            }

            if (toPin.Kind == PinKind.Exec)
            {
                int incoming = graph.Connections.Count(c => c.ToNode == conn.ToNode && c.ToPin == conn.ToPin);
                if (incoming > 1)
                    issues.Add(new ValidationIssue { NodeId = to.Id, Message = $"Exec input '{toPin.Name}' has {incoming} incoming wires (max 1)." });
            }
            else
            {
                var key = (conn.ToNode, conn.ToPin);
                dataInputs[key] = dataInputs.GetValueOrDefault(key) + 1;
            }
        }

        foreach (((string node, string pin), int count) in dataInputs)
        {
            if (count > 1)
                issues.Add(new ValidationIssue { NodeId = node, Message = $"Data input '{pin}' has {count} wires; only the first is used.", IsError = false });
        }

        DetectDataCycles(graph, issues);

        if (!graph.Nodes.OfType<EventNode>().Any())
            issues.Add(new ValidationIssue { Message = "Graph has no event nodes; nothing will run.", IsError = false });

        return issues;
    }

    private static void DetectDataCycles(VisualGraph graph, List<ValidationIssue> issues)
    {
        var edges = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (Connection conn in graph.Connections)
        {
            VSNode? from = graph.FindNode(conn.FromNode);
            if (from is null || from.GetPin(conn.FromPin) is not Pin p || p.Kind != PinKind.Data)
                continue;
            if (!edges.TryGetValue(conn.FromNode, out List<string>? targets))
                edges[conn.FromNode] = targets = [];
            targets.Add(conn.ToNode);
        }

        var state = new Dictionary<string, int>(StringComparer.Ordinal); // 0 unvisited, 1 visiting, 2 done

        bool Visit(string id)
        {
            if (state.TryGetValue(id, out int s))
                return s == 1;
            state[id] = 1;
            if (edges.TryGetValue(id, out List<string>? targets))
            {
                foreach (string next in targets)
                {
                    if (Visit(next))
                        return true;
                }
            }
            state[id] = 2;
            return false;
        }

        foreach (VSNode node in graph.Nodes)
        {
            if (!state.ContainsKey(node.Id) && Visit(node.Id))
            {
                issues.Add(new ValidationIssue { NodeId = node.Id, Message = "Pure data cycle detected (infinite pull recursion)." });
                return;
            }
        }
    }
}
