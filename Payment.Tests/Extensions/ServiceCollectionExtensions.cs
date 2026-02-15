using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Security.Tests.Extensions
{
    /// <summary>
    /// ServiceCollection extensions class.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds a scoped <typeparamref name="TService"/> to middleware with Mocked object instance.
        /// </summary>
        /// <typeparam name="TService">The type of the service to add.</typeparam>
        /// <param name="serviceCollection"></param>
        /// <returns></returns>
        public static IServiceCollection AddMocked<TService>(this IServiceCollection serviceCollection)
            where TService : class => serviceCollection.AddScoped(provider => new Mock<TService>().Object);

        /// <summary>
        /// Adds scoped service od type  <typeparamref name="TService"/> with an implementation type  <typeparamref name="TImplementation"/>.
        /// </summary>
        /// <typeparam name="TService">The type of the service to add.</typeparam>
        /// <typeparam name="TImplementation">The type of the implementation to use.</typeparam>
        /// <param name="serviceCollection"></param>
        /// <returns></returns>
        public static IServiceCollection AddServiceUnderTest<TService, TImplementation>(this IServiceCollection serviceCollection)
            where TService : class
            where TImplementation : class, TService =>
            serviceCollection.AddScoped(typeof(TService), typeof(TImplementation));
    }
}