namespace Faultline.Sdk;

public class FaultlineUnhandledExceptionHook(FaultlineClient client)
{
    private bool _attached;

    public void Attach()
    {
        if (_attached) return;
        _attached = true;

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                // process is terminating right after this — capture must be synchronous
                client.CaptureExceptionAsync(ex, level: "fatal").GetAwaiter().GetResult();
            }
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            client.CaptureExceptionAsync(e.Exception, level: "error").GetAwaiter().GetResult();
            e.SetObserved();
        };
    }
}
