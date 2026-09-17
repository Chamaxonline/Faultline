using System.Text.RegularExpressions;
using Faultline.Contracts;

namespace Faultline.Sdk;

/// <summary>Removes sensitive data from an event before it leaves the process — never sent, not just hidden in the dashboard.</summary>
internal static class FaultlineScrubber
{
    private const string Filtered = "[Filtered]";

    public static void Scrub(ErrorEvent evt, FaultlineOptions options)
    {
        ScrubFields(evt.Tags, options.ScrubFieldNames);
        ScrubFields(evt.Extra, options.ScrubFieldNames);

        if (options.ScrubPatterns.Count == 0) return;

        evt.Message = ScrubText(evt.Message, options.ScrubPatterns);
        evt.StackTrace = evt.StackTrace is null ? null : ScrubText(evt.StackTrace, options.ScrubPatterns);
        evt.UserContext = evt.UserContext is null ? null : ScrubText(evt.UserContext, options.ScrubPatterns);

        foreach (var crumb in evt.Breadcrumbs)
            crumb.Message = ScrubText(crumb.Message, options.ScrubPatterns);
    }

    private static void ScrubFields(Dictionary<string, string> fields, List<string> scrubFieldNames)
    {
        foreach (var key in fields.Keys.ToList())
        {
            if (scrubFieldNames.Any(name => key.Contains(name, StringComparison.OrdinalIgnoreCase)))
                fields[key] = Filtered;
        }
    }

    private static string ScrubText(string text, List<string> patterns)
    {
        foreach (var pattern in patterns)
        {
            try
            {
                text = Regex.Replace(text, pattern, Filtered, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200));
            }
            catch (RegexMatchTimeoutException)
            {
                // a pathological pattern must never block error reporting — skip it for this event
            }
        }
        return text;
    }
}
