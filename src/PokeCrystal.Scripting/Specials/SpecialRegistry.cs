namespace PokeCrystal.Scripting.Specials;

/// <summary>
/// Registry of named special handlers. Keyed by the special's name string.
/// </summary>
public sealed class SpecialRegistry
{
    private readonly Dictionary<string, ISpecialHandler> _handlers = new();

    public void Register(ISpecialHandler handler)
        => _handlers[handler.Key] = handler;

    public void Execute(string key, IScriptContext ctx)
    {
        if (!_handlers.TryGetValue(key, out var handler))
            throw new KeyNotFoundException($"Special '{key}' not registered.");
        handler.Execute(ctx);
    }

    public bool TryExecute(string key, IScriptContext ctx)
    {
        if (!_handlers.TryGetValue(key, out var handler)) return false;
        handler.Execute(ctx);
        return true;
    }
}
