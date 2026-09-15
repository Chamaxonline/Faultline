using Faultline.Domain.Contracts;

namespace Faultline.Sdk;

/// <summary>
/// Ambient state (tags, user, breadcrumbs) that flows through the current async
/// call chain and gets attached to whatever error is captured next — mirrors
/// Sentry's Scope concept. <see cref="Push"/> starts an isolated child scope (e.g.
/// per HTTP request); mutations outside a pushed scope go to the shared root scope.
/// </summary>
public static class FaultlineScope
{
    private const int MaxBreadcrumbs = 50;
    private static readonly AsyncLocal<ScopeData> _current = new();

    public static ScopeData Current => _current.Value ??= new ScopeData();

    /// <summary>Starts a child scope cloned from the current one; disposing restores the parent.</summary>
    public static IDisposable Push()
    {
        var previous = _current.Value;
        _current.Value = Current.Clone();
        return new PopToken(previous);
    }

    public static void SetTag(string key, string value) => Current.Tags[key] = value;

    public static void SetUser(string user) => Current.UserContext = user;

    public static void SetExtra(string key, string value) => Current.Extra[key] = value;

    public static void AddBreadcrumb(string message, string category = "manual", string level = "info")
    {
        var scope = Current;
        scope.Breadcrumbs.AddLast(new Breadcrumb { Message = message, Category = category, Level = level });
        while (scope.Breadcrumbs.Count > MaxBreadcrumbs)
            scope.Breadcrumbs.RemoveFirst();
    }

    public static void Clear() => _current.Value = new ScopeData();

    private sealed class PopToken(ScopeData? previous) : IDisposable
    {
        public void Dispose() => _current.Value = previous!;
    }
}

public class ScopeData
{
    public Dictionary<string, string> Tags { get; private set; } = [];
    public Dictionary<string, string> Extra { get; private set; } = [];
    public string? UserContext { get; set; }
    public LinkedList<Breadcrumb> Breadcrumbs { get; private set; } = [];

    public ScopeData Clone() => new()
    {
        Tags = new Dictionary<string, string>(Tags),
        Extra = new Dictionary<string, string>(Extra),
        UserContext = UserContext,
        Breadcrumbs = new LinkedList<Breadcrumb>(Breadcrumbs)
    };
}
