using Security.Domain;
using Security.Repositories.impl;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.EntityFrameworkCore;
using Security.Data.EF.Infrastructure;
using Security.Shared;


namespace Security.Tests.RepositoryTest
{
    [TestClass]
    public class UserRepositoryTests : RepositoryIntegrationTestBase
    {
        private UserRepository _userRepository;

        protected async Task SeedDataAsync(Guid id , string name = "TestUSer", string email = "user1@domain.com")
        {
            var user1 = new User
            {
                Id = id,
                UserName = name,
                Email = email,
                FullName = "SeedFullName1",
                MobileNumber = "+38888888",
                Language = "MK1",
                Culture = "SLO1",
                PasswordHash = [],
                PasswordSalt = [],
            };

            await _dbContext.Users.AddAsync(user1);
            await _dbContext.SaveChangesAsync();
        }

        [TestMethod]
        public async Task UserNameExists_ShouldReturnTrue_WhenUserExists()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var username = "TestUser";

            await SeedDataAsync(userId, username);

            _userRepository = new UserRepository(_dbContext);

            // Act
            var result = await _userRepository.UserNameExists(username);

            // Assert
            Assert.IsTrue(result, "Expected UserNameExists to return true for existing user.");
        }

        [TestMethod]
        public async Task UserNameExists_ShouldReturnFalse_WhenUserDoesNotExist()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var username = "TestUser";

            await SeedDataAsync(userId, username);

            _userRepository = new UserRepository(_dbContext);

            // Act
            var result = await _userRepository.UserNameExists("NonExistingUser");

            // Assert
            Assert.IsFalse(result, "Expected UserNameExists to return false for non-existing user.");
        }

        [TestMethod]
        public async Task UserNameExists_ShouldThrowException_WhenDatabaseUnavailable()
        {
            // Arrange
            var disposedContext = _dbContext;
            await disposedContext.DisposeAsync();

            _userRepository = new UserRepository(disposedContext);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ObjectDisposedException>(async () =>
                await _userRepository.UserNameExists("Alice"));
        }

        [TestMethod]
        public async Task EmailExists_ShouldReturnTrue_WhenEmailExists()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var userName = "user1";
            var email = "test@email.com";

            await SeedDataAsync(userId, userName, email);
            
            _userRepository = new UserRepository(_dbContext);

            // Act
            var result = await _userRepository.EmailExists(email);

            // Assert
            Assert.IsTrue(result, "Expected EmailExists to return true when email exists in database.");
        }

        [TestMethod]
        public async Task EmailExists_ShouldReturnFalse_WhenEmailDoesNotExist()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var userName = "user1";
            var email = "test@email.com";

            await SeedDataAsync(userId, userName, email);

            _userRepository = new UserRepository(_dbContext);

            // Act
            var result = await _userRepository.EmailExists("nonexistent@domain.com");

            // Assert
            Assert.IsFalse(result, "Expected EmailExists to return false when email does not exist in database.");
        }

        [TestMethod]
        public async Task EmailExists_ShouldThrowException_WhenDatabaseDisposed()
        {
            // Arrange
            await _dbContext.DisposeAsync();
            _userRepository = new UserRepository(_dbContext);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ObjectDisposedException>(async () =>
                await _userRepository.EmailExists("alice@domain.com"));
        }

        [TestMethod]
        public async Task Add_ShouldInsertUser()
        {
            // Arrange
            var user = new User
            {
                Id = Guid.NewGuid(),
                UserName = "TestUser",
                Email = "testuser@outlook.com",
                FullName = "SeedFullName1",
                MobileNumber = "+38888888",
                Language = "MK1",
                Culture = "SLO1",
                PasswordHash = [],
                PasswordSalt = [],
            };


            _userRepository = new UserRepository(_dbContext);
         
            // Act
            await _userRepository.Add(user);
            var found = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id== user.Id);

            // Assert
            Assert.IsNotNull(found);
            Assert.AreEqual(user.UserName, found.UserName);
            Assert.AreEqual(user.Email, found.Email);
        }

        [TestMethod]
        public async Task Add_ShouldThrowRepositoryException_OnSqlError()
        {
            // Arrange
            var user = new User
            {
                Id = Guid.NewGuid(),
                UserName = "TestUser",
                Email = "testuser@outlook.com",
                FullName = "SeedFullName1",
                MobileNumber = "+38888888",
                Language = "MK1",
                Culture = "SLO1",
                PasswordHash = [],
                PasswordSalt = [],
            };


            await _dbContext.DisposeAsync();
            _userRepository = new UserRepository(_dbContext);

            // Act & Assert
            var ex = await Assert.ThrowsExceptionAsync<RepositoryException>(
                async () => await _userRepository.Add(user));

            Assert.IsTrue(ex.Message.Contains("SQL Error 2627"));
        }

        [TestMethod]
        public async Task GetById_ShouldReturnUser_WhenIdExists()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var userName = "user1";
            var email = "test@email.com";

            await SeedDataAsync(userId, userName, email);
            _userRepository = new UserRepository(_dbContext);

            // Act
            var result = await _userRepository.GetById(userId);

            // Assert
            Assert.IsNotNull(result, "Expected a user to be returned for a valid ID.");
            Assert.AreEqual(email, result.Email);
            Assert.AreEqual(userName, result.UserName);
        }

        [TestMethod]
        public async Task GetById_ShouldReturnNull_WhenIdDoesNotExist()
        {
            // Arrange
            _userRepository = new UserRepository(_dbContext);
            var nonExistingId = Guid.NewGuid();

            // Act
            var result = await _userRepository.GetById(nonExistingId);

            // Assert
            Assert.IsNull(result, "Expected null when user ID does not exist.");
        }

        [TestMethod]
        public async Task GetById_ShouldThrowException_WhenDbContextFails()
        {
            // Arrange
            var testId = Guid.NewGuid();
            await _dbContext.DisposeAsync();
            _userRepository = new UserRepository(_dbContext);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                async () => await _userRepository.GetById(testId));
        }


        [TestMethod]
        public async Task Update_ShouldModifyExistingUser()
        {
            // Arrange
            _userRepository = new UserRepository(_dbContext);

            var userToUpdate = await _dbContext.Users.FirstAsync();
            userToUpdate.FullName = "Updated Name";

            // Act
            var result = await _userRepository.Update(userToUpdate);

            // Assert
            Assert.IsTrue(result, "Expected Update to return true when data is changed.");

            var updated = await _dbContext.Users.AsNoTracking().FirstAsync();
            Assert.AreEqual(userToUpdate.FullName, updated.FullName, "User full name should be updated in the database.");
        }

        [TestMethod]
        public async Task Update_ShouldReturnFalse_WhenNothingChanged()
        {
            // Arrange
            _userRepository = new UserRepository(_dbContext);
            var user = await _dbContext.Users.AsNoTracking().FirstAsync();

            // Act
            var result = await _userRepository.Update(user);

            // Assert
            Assert.IsFalse(result, "Expected Update to return false when no changes were made.");
        }

        [TestMethod]
        public async Task Update_ShouldNotThrow_WhenUpdatingNonExistingUser()
        {
            // Arrange
            _userRepository = new UserRepository(_dbContext);

            var nonExistingUser = new User
            {
                Id = Guid.NewGuid(),
                UserName = "Ghost",
                Email = "ghost@domain.com",
                FullName = "ShouldNotExist"
            };

            // Act
            var result = await _userRepository.Update(nonExistingUser);

            // Assert
            Assert.IsTrue(result, "EF InMemory will treat non-existing entities as added; expect SaveChanges > 0.");
        }

        [TestMethod]
        public async Task Update_ShouldThrowRepositoryException_OnSqlError()
        {
            // Arrange
            var user = new User
            {
                Id = Guid.NewGuid(),
                UserName = "TestUser",
                Email = "testuser@outlook.com",
                FullName = "SeedFullName1",
                MobileNumber = "+38888888",
                Language = "MK1",
                Culture = "SLO1",
                PasswordHash = [],
                PasswordSalt = [],
            };


            await _dbContext.DisposeAsync();
            _userRepository = new UserRepository(_dbContext);

            // Act & Assert
            var ex = await Assert.ThrowsExceptionAsync<RepositoryException>(
                async () => await _userRepository.Update(user));

            Assert.IsTrue(ex.Message.Contains("SQL Error 2627"));
        }

        [TestMethod]
        public async Task Delete_ShouldRemoveExistingUser()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var userName = "user1";
            var email = "test@email.com";

            await SeedDataAsync(userId, userName, email);
            _userRepository = new UserRepository(_dbContext);

            // Act
            var result = await _userRepository.Delete(userId);
            var exists = await _dbContext.Users.AnyAsync(u => u.Id == userId);

            // Assert
            Assert.IsTrue(result, "Expected Delete to return true for existing user.");
            Assert.IsFalse(exists, "User should be removed from database.");
        }

        [TestMethod]
        public async Task Delete_ShouldReturnFalse_WhenUserDoesNotExist()
        {
            // Arrange
            _userRepository = new UserRepository(_dbContext);
            var nonExistingId = Guid.NewGuid();

            // Act
            var result = await _userRepository.Delete(nonExistingId);

            // Assert
            Assert.IsFalse(result, "Expected Delete to return false when user not found.");
        }

        [TestMethod]
        public async Task Delete_ShouldNotAffectOtherRecords()
        {
            // Arrange
            // Arrange
            var userId = Guid.NewGuid();
            var userName = "user1";
            var email = "test@email.com";

            await SeedDataAsync(userId, userName, email);
            _userRepository = new UserRepository(_dbContext);

            var anotherUser = new User
            {
                Id = Guid.NewGuid(),
                UserName = "KeepUser",
                Email = "keep@domain.com"
            };

            await _dbContext.Users.AddAsync(anotherUser);
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _userRepository.Delete(userId);

            // Assert
            Assert.IsTrue(result);
            Assert.IsFalse(await _dbContext.Users.AnyAsync(u => u.Id == userId));
            Assert.IsTrue(await _dbContext.Users.AnyAsync(u => u.Id == anotherUser.Id),
                "Other users should remain untouched.");
        }

        [TestMethod]
        public async Task Delete_ShouldThrowArgumentException_OnSqlError()
        {
            // Arrange
            await _dbContext.DisposeAsync();
            _userRepository = new UserRepository(_dbContext);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentException>(async () =>
                await _userRepository.Delete(Guid.Empty));
        }
    }
}