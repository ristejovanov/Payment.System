using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Payment.GT.Classes;
using Payment.GT.Classes.Impl;
using Payment.GT.Classes.Interface;
using Payment.Protocol.Dtos;
using Payment.Protocol.DtoValidations;
using Payment.Protocol.Impl;
using Payment.Protocol.Interface;
using Serilog;
using Serilog.Debugging;


SelfLog.Enable(msg => System.Diagnostics.Debug.WriteLine(msg)); // helps diagnose config issues

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();


try
{
    var builder = Host.CreateDefaultBuilder(args)
        .UseSerilog((context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext();
        })
        .ConfigureServices((context, services) =>
        {
            services.AddMemoryCache();

            // Gateway core services (Singleton)
            services.AddSingleton<IGatewayProcessor, GatewayProcessor>();
            services.AddSingleton<IGatewayStateStore, GatewayStateStore>();
            services.AddSingleton<IIssuerClient, MockIssuerClient>();

            // Protocol services (Singleton - stateless)
            services.AddSingleton<IFrameOperator, FrameOperator>();
            services.AddSingleton<ITlvMapper, TlvMapper>();
            services.AddSingleton<IObjectCreator, ObjectCreator>();

            // Validators (Singleton - stateless)
            services.AddSingleton<IMessageValidator<A70RequestDto>, A70RequestValidator>();
            services.AddSingleton<IMessageValidator<A72RequestDto>, A72RequestValidator>();

            // TCP Server (Hosted Service)
            services.AddHostedService<TcpServerHostedService>();
        });

    // ← THESE TWO LINES WERE MISSING
    var host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "GT host terminated unexpectedly");
}
finally
{
    // ← Flush all logs before exit
    await Log.CloseAndFlushAsync();
}