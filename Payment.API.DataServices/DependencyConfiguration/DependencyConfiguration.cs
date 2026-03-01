using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace Security.DataServices.DependencyConfiguration
{
    [ExcludeFromCodeCoverage]
    public static class DependencyConfiguration
    {
        public static void InstallDependency(this IServiceCollection services)
        {
            /*Services*/
            //services.AddScoped<IClientService, ClientService>();
            //services.AddScoped<IUserService, UserService>();
            
            ///*Helpers*/
            //services.AddSingleton<IHelper, Helper>();

            ///*Repositories*/
            //services.AddScoped<IClientRepository, ClientRepository>();
            //services.AddScoped<IUserRepository, UserRepository>();


            //services.AddHostedService<AdminClientService>();
        }
    }
}
