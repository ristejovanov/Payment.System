using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Security.Tests.ServicesTest
{
    public class UnitTestBase
    {
        protected IServiceProvider ServiceProvider;

        /// <summary>
        /// Initializes the test by setting up the dependency injection service provider.
        /// </summary>
        [TestInitialize]
        public void TestInitialize()
        {
            ServiceProvider = SetupDependencies();
        }

        /// <summary>
        /// Sets needed dependencies for ServiceProvider.
        /// </summary>
        /// <returns></returns>
        private IServiceProvider SetupDependencies()
        {
            var serviceCollection = new ServiceCollection();

            AddDependencies(serviceCollection);

            return serviceCollection.BuildServiceProvider();
        }

        /// <summary>
        /// Adds the core dependencies for the unit test. 
        /// Override this method in derived classes to add specific services under test or mocks.
        /// </summary>
        /// <param name = "serviceCollection" ></ param >
        protected virtual void AddDependencies(ServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<ILoggerFactory, LoggerFactory>();
            serviceCollection.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        }
    }
}
