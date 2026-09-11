using System.Security.Cryptography;
using System.Text;
using Faultline.Domain.Contracts;

namespace Faultline.Domain;

public static class Fingerprint
{
    private const int TopFrameCount = 5;

    /// <summary>
    /// Groups events into the same Issue when they share exception type + normalized
    /// message + the top N stack frames. Line numbers are excluded so the same bug
    /// still groups after unrelated code shifts move a line by a few rows.
    /// </summary>
    public static string Compute(ErrorEvent evt)
    {
        var sb = new StringBuilder();
        sb.Append(evt.ExceptionType).Append('|');
        sb.Append(NormalizeMessage(evt.Message)).Append('|');

        foreach (var frame in evt.Frames.Take(TopFrameCount))
        {
            sb.Append(frame.Function).Append(':').Append(frame.File).Append(';');
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static string NormalizeMessage(string message) =>
        System.Text.RegularExpressions.Regex.Replace(message, @"\d+", "#");
}
