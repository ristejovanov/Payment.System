using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Payment.GT.Classes;
using Payment.GT.Classes.Impl;
using Payment.GT.Classes.Interface;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddSingleton<IGatewayStateStore, GatewayStateStore>();
        builder.Services.AddSingleton<IIssuerClient, MockIssuerClient>();
        builder.Services.AddSingleton<GatewayProcessor>();
        builder.Services.AddHostedService<TcpServerHostedService>();

        var host = builder.Build();
        await host.RunAsync();
    }
}