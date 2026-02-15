using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Security.Data.EF.Infrastructure;

namespace Security.Tests.RepositoryTest
{
    /// <summary>
    /// Base class for repository integration tests using MSTest.
    /// Sets up an isolated in-memory database per test and provides async seeding.
    /// </summary>
    public abstract class RepositoryIntegrationTestBase
    {
        protected ServiceProvider _serviceProvider;
        protected AppDbContext _dbContext;

        [TestInitialize]
        public async Task TestInitialize()
        {
            // Create new service provider for each test
            var services = new ServiceCollection();

            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(Guid.NewGuid().ToString())); // Unique DB per test

            _serviceProvider = services.BuildServiceProvider();
            _dbContext = _serviceProvider.GetRequiredService<AppDbContext>();

            await _dbContext.Database.EnsureCreatedAsync();

        }

        [TestCleanup]
        public async Task TestCleanup()
        {
            if (_dbContext != null)
            {
                await _dbContext.DisposeAsync();
            }

            if (_serviceProvider != null)
                await _serviceProvider.DisposeAsync();
        }
    }
}