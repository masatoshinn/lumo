namespace Lumo.Engine.VisualScripting;

/// <summary>
/// Base class for all visual-scripting nodes. Pin layout is declared in the
/// constructor; literal input values are stored as raw invariant strings in
/// <see cref="Values"/> and converted on read by the interpreter.
/// </summary>
public abstract class VSNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string TypeId { get; internal set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public List<Pin> Pins { get; } = [];
    public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);

    public Pin? GetPin(string name) => Pins.FirstOrDefault(p => p.Name == name);
    public IEnumerable<Pin> InputPins => Pins.Where(p => p.Direction == PinDirection.Input);
    public IEnumerable<Pin> OutputPins => Pins.Where(p => p.Direction == PinDirection.Output);

    protected void ExecIn(string name = "in") => Pins.Add(new Pin
    {
        Name = name,
        Kind = PinKind.Exec,
        DataType = PinDataType.Exec,
        Direction = PinDirection.Input
    });

    protected void ExecOut(string name) => Pins.Add(new Pin
    {
        Name = name,
        Kind = PinKind.Exec,
        DataType = PinDataType.Exec,
        Direction = PinDirection.Output
    });

    protected void DataIn(string name, PinDataType type, string def = "")
    {
        Pins.Add(new Pin
        {
            Name = name,
            Kind = PinKind.Data,
            DataType = type,
            Direction = PinDirection.Input
        });
        Values[name] = def;
    }

    protected void DataOut(string name, PinDataType type) => Pins.Add(new Pin
    {
        Name = name,
        Kind = PinKind.Data,
        DataType = type,
        Direction = PinDirection.Output
    });

    /// <summary>
    /// Executes an exec-flow node. The node signals successor pins via
    /// <see cref="GraphContext.Emit"/>.
    /// </summary>
    public virtual void Execute(GraphContext ctx)
    {
    }

    /// <summary>
    /// Evaluates a pure-data output pin (pull model).
    /// </summary>
    public virtual object? EvaluateOutput(GraphContext ctx, string pin) => null;

    /// <summary>
    /// Resets transient per-run state (e.g. Do Once latch).
    /// </summary>
    public virtual void Reset()
    {
    }
}
