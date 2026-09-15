namespace Faultline.Sdk;

public class FaultlineOptions
{
    /// <summary>Ingestion API base URL, e.g. https://faultline.internal.bistec.com</summary>
    public string ServerUrl { get; set; } = default!;

    /// <summary>Project public key (from the Faultline dashboard).</summary>
    public string ProjectKey { get; set; } = default!;

    public string Environment { get; set; } = "production";
    public string? Release { get; set; }

    /// <summary>
    /// Tag/extra keys scrubbed before an event leaves the process (case-insensitive
    /// substring match — "authToken" matches "token"). Values are replaced with
    /// "[Filtered]", never sent. Add to this list rather than replacing it, unless
    /// you specifically want to allow one of the defaults through.
    /// </summary>
    public List<string> ScrubFieldNames { get; set; } =
        ["password", "token", "authorization", "connectionstring", "secret", "apikey"];

    /// <summary>
    /// Regex patterns run against free-text fields (message, stack trace, user
    /// context, breadcrumb messages) — any match is replaced with "[Filtered]".
    /// Empty by default; add patterns for anything your app might log verbatim
    /// (card numbers, internal tokens, etc.).
    /// </summary>
    public List<string> ScrubPatterns { get; set; } = [];

    /// <summary>
    /// Assembly name prefixes considered "your app" for the in-app/library frame
    /// split in stack traces. Defaults to the entry assembly's name — add your
    /// other project assemblies (e.g. a shared "Acme.Core") if you have more than one.
    /// </summary>
    public List<string> InAppAssemblyPrefixes { get; set; } =
        [System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? string.Empty];

    /// <summary>Lines of source shown before/after the failing line when the source file is available on disk. 0 disables source context.</summary>
    public int ContextLineCount { get; set; } = 3;
}
