using Faultline.Infrastructure;
using Faultline.Infrastructure.Alerts;
using Faultline.Infrastructure.Queue;
using Faultline.Worker;
using Microsoft.EntityFrameworkCore;
using Serilog;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .CreateLogger();
builder.Services.AddSerilog();

builder.Services.AddDbContext<FaultlineDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));
builder.Services.AddSingleton<IEventQueue, RedisEventQueue>();

builder.Services.Configure<AlertOptions>(builder.Configuration.GetSection("Alerts"));
builder.Services.AddHttpClient<IAlertNotifier, TeamsAlertNotifier>();

builder.Services.AddHostedService<EventGroupingWorker>();

var host = builder.Build();
host.Run();
