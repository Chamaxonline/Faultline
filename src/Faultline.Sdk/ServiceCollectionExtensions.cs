using Faultline.Sdk.Reliability;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;

namespace Faultline.Sdk;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers FaultlineClient and wires AppDomain.UnhandledException so unhandled
    /// crashes are reported even if the app has no explicit try/catch around them.
    /// </summary>
    public static IServiceCollection AddFaultline(this IServiceCollection services, Action<FaultlineOptions> configure)
    {
        services.Configure(configure);

        services.AddHttpClient<FaultlineClient>((sp, client) =>
            {
                var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<FaultlineOptions>>().Value;
                client.BaseAddress = new Uri(options.ServerUrl);
                client.Timeout = TimeSpan.FromSeconds(5);
            })
            .AddTransientHttpErrorPolicy(builder => builder.WaitAndRetryAsync(
                3, attempt => TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt))));

        services.AddSingleton<FaultlineUnhandledExceptionHook>();

        // no-op if the host never runs the generic host's hosted services (e.g. a
        // console app that never calls host.RunAsync) — harmless either way
        services.AddHostedService<FaultlineOfflineQueueRetryService>();
        services.AddHostedService<FaultlineClientReportService>();

        return services;
    }

    public static IServiceProvider UseFaultlineUnhandledExceptionCapture(this IServiceProvider serviceProvider)
    {
        serviceProvider.GetRequiredService<FaultlineUnhandledExceptionHook>().Attach();
        return serviceProvider;
    }
}
