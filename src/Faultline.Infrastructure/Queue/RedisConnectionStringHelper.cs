using StackExchange.Redis;

namespace Faultline.Infrastructure.Queue;

/// <summary>
/// Accepts either StackExchange.Redis's native options-string format
/// ("host:port,password=x,ssl=true") or a redis://user:pass@host:port URI (what
/// most managed providers, e.g. Upstash, hand you) and produces ConfigurationOptions
/// with AbortOnConnectFail forced off — a transient outage (e.g. a free-tier Redis
/// still waking up) must not crash the whole process on startup.
/// </summary>
public static class RedisConnectionStringHelper
{
    public static ConfigurationOptions ToOptions(string connectionString)
    {
        var normalized = connectionString.Contains("://") ? FromUri(connectionString) : connectionString;

        var options = ConfigurationOptions.Parse(normalized);
        options.AbortOnConnectFail = false;
        return options;
    }

    private static string FromUri(string connectionString)
    {
        var uri = new Uri(connectionString);
        var useSsl = uri.Scheme == "rediss";

        string? password = null;
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var parts = uri.UserInfo.Split(':', 2);
            password = parts.Length > 1 ? parts[1] : parts[0];
        }

        var result = $"{uri.Host}:{uri.Port}";
        if (!string.IsNullOrEmpty(password))
            result += $",password={password}";
        if (useSsl)
            result += ",ssl=true";

        return result;
    }
}
