using Faultline.Domain;
using Faultline.Contracts;
using Xunit;

namespace Faultline.Tests;

public class FingerprintTests
{
    [Fact]
    public void SameExceptionAndFrames_ProduceSameFingerprint()
    {
        var a = MakeEvent("NullReferenceException", "Object reference not set to an instance of an object at line 42");
        var b = MakeEvent("NullReferenceException", "Object reference not set to an instance of an object at line 99");

        Assert.Equal(Fingerprint.Compute(a), Fingerprint.Compute(b));
    }

    [Fact]
    public void DifferentExceptionType_ProducesDifferentFingerprint()
    {
        var a = MakeEvent("NullReferenceException", "boom");
        var b = MakeEvent("ArgumentException", "boom");

        Assert.NotEqual(Fingerprint.Compute(a), Fingerprint.Compute(b));
    }

    [Fact]
    public void DifferentTopFrames_ProduceDifferentFingerprint()
    {
        var a = MakeEvent("Exception", "boom", ("HandlerA", "a.cs"));
        var b = MakeEvent("Exception", "boom", ("HandlerB", "b.cs"));

        Assert.NotEqual(Fingerprint.Compute(a), Fingerprint.Compute(b));
    }

    private static ErrorEvent MakeEvent(string type, string message, params (string fn, string file)[] frames)
    {
        var evt = new ErrorEvent { ExceptionType = type, Message = message };
        foreach (var (fn, file) in frames)
            evt.Frames.Add(new StackFrame { Function = fn, File = file });
        return evt;
    }
}
