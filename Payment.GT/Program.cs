using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Payment.GT.Classes;
using Payment.GT.Classes.Impl;
using Payment.GT.Classes.Interface;
using Payment.Protocol.Impl;
using Payment.Protocol.Interface;
using Payment.Protocol.DtoValidations;
using Payment.Protocol.Dtos;
using Serilog;

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

        // Gateway core services (all Singleton - correct!)
        services.AddSingleton<IGatewayProcessor, GatewayProcessor>();
        services.AddSingleton<IGatewayStateStore, GatewayStateStore>();
        services.AddSingleton<IConnectionHandler, ConnectionHandler>();
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

var host = builder.Build();
await host.RunAsync();