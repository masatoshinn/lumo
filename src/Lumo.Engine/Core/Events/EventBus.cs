namespace Lumo.Engine.Core;

/// <summary>
/// Lightweight event system for inter-system communication.
/// </summary>
public sealed class EventBus : IDisposable
{
    private readonly Dictionary<Type, List<Delegate>> _handlers = new();
    private bool _isDisposed;

    /// <summary>
    /// Subscribe to an event type.
    /// Returns an IDisposable that unsubscribes when disposed.
    /// </summary>
    public IDisposable Subscribe<T>(Action<T> handler) where T : struct
    {
        var type = typeof(T);
        if (!_handlers.TryGetValue(type, out var list))
        {
            list = new List<Delegate>();
            _handlers[type] = list;
        }
        list.Add(handler);

        return new Subscription(() =>
        {
            if (!_isDisposed && _handlers.TryGetValue(type, out var l))
                l.Remove(handler);
        });
    }

    /// <summary>
    /// Raise an event to all subscribers.
    /// </summary>
    public void Raise<T>(T evt) where T : struct
    {
        if (_isDisposed)
            return;

        if (!_handlers.TryGetValue(typeof(T), out var list))
            return;

        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i] is Action<T> action)
                action.Invoke(evt);
        }
    }

    public void Clear()
    {
        _handlers.Clear();
    }

    public void Dispose()
    {
        _isDisposed = true;
        _handlers.Clear();
    }

    private sealed class Subscription(Action unsubscribe) : IDisposable
    {
        public void Dispose() => unsubscribe();
    }
}
