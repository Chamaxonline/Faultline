using Faultline.Infrastructure.Queue;
using Xunit;

namespace Faultline.Tests;

public class RedisConnectionStringHelperTests
{
    [Fact]
    public void OptionsStringFormat_ParsesHostAndPort()
    {
        var options = RedisConnectionStringHelper.ToOptions("localhost:6379");
        Assert.Equal("localhost:6379", options.EndPoints[0].ToString()!.Replace("Unspecified/", ""));
    }

    [Fact]
    public void RedissUri_ConvertsToSslOptions()
    {
        var options = RedisConnectionStringHelper.ToOptions("rediss://default:my-pass@example-upstash.io:6380");
        Assert.Equal("my-pass", options.Password);
        Assert.True(options.Ssl);
    }

    [Fact]
    public void RedisUri_WithoutSsl_LeavesSslOff()
    {
        var options = RedisConnectionStringHelper.ToOptions("redis://default:my-pass@localhost:6379");
        Assert.Equal("my-pass", options.Password);
        Assert.False(options.Ssl);
    }

    [Fact]
    public void Uri_WithoutPassword_LeavesPasswordNull()
    {
        var options = RedisConnectionStringHelper.ToOptions("redis://localhost:6379");
        Assert.Null(options.Password);
    }

    [Fact]
    public void AnyInputFormat_AlwaysDisablesAbortOnConnectFail()
    {
        Assert.False(RedisConnectionStringHelper.ToOptions("localhost:6379").AbortOnConnectFail);
        Assert.False(RedisConnectionStringHelper.ToOptions("redis://localhost:6379").AbortOnConnectFail);
        Assert.False(RedisConnectionStringHelper.ToOptions("localhost:6379,abortConnect=true").AbortOnConnectFail);
    }
}
