using Faultline.Sdk;
using Xunit;

namespace Faultline.Tests;

public class FaultlineScopeTests
{
    public FaultlineScopeTests() => FaultlineScope.Clear();

    [Fact]
    public void SetTag_IsVisibleOnCurrentScope()
    {
        FaultlineScope.SetTag("env", "test");
        Assert.Equal("test", FaultlineScope.Current.Tags["env"]);
    }

    [Fact]
    public void Push_IsolatesChildMutations_FromParent()
    {
        FaultlineScope.SetTag("outer", "1");

        using (FaultlineScope.Push())
        {
            FaultlineScope.SetTag("inner", "2");
            Assert.True(FaultlineScope.Current.Tags.ContainsKey("outer"), "child should inherit parent's existing tags");
            Assert.True(FaultlineScope.Current.Tags.ContainsKey("inner"));
        }

        Assert.False(FaultlineScope.Current.Tags.ContainsKey("inner"), "parent must not see child-only mutations after pop");
    }

    [Fact]
    public void AddBreadcrumb_CapsAtFifty()
    {
        for (var i = 0; i < 60; i++)
            FaultlineScope.AddBreadcrumb($"event {i}");

        Assert.Equal(50, FaultlineScope.Current.Breadcrumbs.Count);
        Assert.Equal("event 59", FaultlineScope.Current.Breadcrumbs.Last!.Value.Message);
    }
}
