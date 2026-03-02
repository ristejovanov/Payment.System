using System.Diagnostics.CodeAnalysis;
using ATM.DataServices.interfaces;
using AtmService.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Payment.API.DataServices.impl.Helpers;
using Payment.API.DataServices.interfaces.Helpers;
using Payment.Hubs;
using Payment.Protocol.DtoValidations;
using Payment.Protocol.Dtos;
using Payment.Protocol.Impl;
using Payment.Protocol.Interface;
using Payment.Shared.Dto;

namespace Payment.API.DataServices.DependencyConfiguration
{
    [ExcludeFromCodeCoverage]
    public static class DependencyConfiguration
    {
        public static void InstallDependency(this IServiceCollection services, IConfiguration configuration)
        {
            // === Business Services (Scoped - per HTTP request) ===
            services.AddScoped<IWithdrawalService, WithdrawalsService>();
            services.AddScoped<IEventPublisher, EventPublisher>();
            
            // === Gateway Client Services (Scoped) ===
            services.AddScoped<IGtClient, GtClient>();
            services.AddScoped<IGtConnection, GtConnection>();

            // Configure GtClientOptions from appsettings
            services.Configure<GtClientOptions>(configuration.GetSection("GatewayClient"));
            
            // === Helper Services (Singleton - stateless) ===
            services.AddSingleton<IStenGenerator, StanGenerator>();
            
            // === Protocol Services (Singleton - stateless/caching) ===
            services.AddSingleton<IFrameOperator, FrameOperator>();
            services.AddSingleton<IObjectCreator, ObjectCreator>();
            services.AddSingleton<ITlvMapper, TlvMapper>();
            
            // === Validators (Singleton - stateless) ===
            services.AddSingleton<IMessageValidator<A70RequestDto>, A70RequestValidator>();
            services.AddSingleton<IMessageValidator<A72RequestDto>, A72RequestValidator>();
            
            // === SignalR ===
            services.AddSignalR();
            
            // === Memory Cache ===
            services.AddMemoryCache();
        }
    }
}
