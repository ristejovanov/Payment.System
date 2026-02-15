using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Security.DataServices.impl;
using Security.DataServices.interfaces;
using Security.DataServices.interfaces.Helpers;
using Security.Domain;
using Security.Repositories.interfaces;
using Security.Shared;
using Security.Shared.Dtos;
using Security.Shared.Requests;
using Security.Tests.Extensions;

namespace Security.Tests.ServicesTest
{
    [TestClass]
    public class UserServiceTests : UnitTestBase
    {
        private Mock<IUserRepository> _repoMock;
        private Mock<IHelper> _helperMock;

        protected override void AddDependencies(ServiceCollection serviceCollection)
        {
            base.AddDependencies(serviceCollection);
            serviceCollection.AddServiceUnderTest<IUserService, UserService>();
            serviceCollection.AddMocked<IUserRepository>();
            serviceCollection.AddMocked<IHelper>();
            serviceCollection.AddMocked<IMapper>();
        }

        [TestMethod]
        public async Task Create_Should_Create_User_Successfully()
        {
            var Email = "test@domain.com";
            var userName = "Name";

            
            // Arrange
            var req = new UserRequest
            {
                Email = Email,
                UserName = userName,
                FullName = "User Name",
                MobileNumber = "123",
                Language = "en",
                Culture = "en-US",
                Password = "pass"
            };

            var createUserDto = new CreateUserDto
            {
                Id = Guid.NewGuid(),
                UserName = userName
            };

            var userRepositoryMock = ServiceProvider.GetService<IUserRepository>() as IMocked<IUserRepository>;
            userRepositoryMock!.Mock.SetupAllProperties();
            userRepositoryMock.Mock.Setup(x => x.Add(It.IsAny<User>()));
            userRepositoryMock.Mock.Setup(x => x.UserNameExists(It.IsAny<string>())).ReturnsAsync(false);
            userRepositoryMock.Mock.Setup(x => x.EmailExists(It.IsAny<string>())).ReturnsAsync(false);


            var expectedHash = new byte[] { 1, 2, 3 };
            var expectedSalt = new byte[] { 4, 5, 6 };

            var helperMock = ServiceProvider.GetService<IHelper>() as IMocked<IHelper>;
            helperMock!.Mock.SetupAllProperties();
            helperMock.Mock.Setup(x => x.Hash(It.IsAny<string>()))
                .Returns((expectedHash,expectedSalt));

            var mapperMock = ServiceProvider.GetService<IMapper>() as IMocked<IMapper>;
            mapperMock!.Mock.SetupAllProperties();
            mapperMock.Mock.Setup(m => m.Map<CreateUserDto>(It.IsAny<User>()))
                .Returns(createUserDto);

            var serviceUnderTest = ServiceProvider.GetService<IUserService>();

            // Act
            var result = await serviceUnderTest!.Create(req);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(req.UserName, result.UserName);

            userRepositoryMock.Mock.Verify(mock => mock.Add(It.IsAny<User>()), Times.Once);
            userRepositoryMock.Mock.Verify(mock => mock.UserNameExists(It.IsAny<string>()), Times.Once);
            userRepositoryMock.Mock.Verify(mock => mock.EmailExists(It.IsAny<string>()), Times.Once);
            //etc for all methods in the row 
        }

        [TestMethod]
        [ExpectedException(typeof(ConflictException))]
        public async Task Create_Should_Throw_When_EmailExists()
        {
            // Arrange
            var Email = "test@domain.com";
            var userName = "Name";

            var req = new UserRequest
            {
                Email = Email,
                UserName = userName,
                FullName = "User Name",
                MobileNumber = "123",
                Language = "en",
                Culture = "en-US",
                Password = "pass"
            };

            var userRepositoryMock = ServiceProvider.GetService<IUserRepository>() as IMocked<IUserRepository>;
            userRepositoryMock!.Mock.SetupAllProperties();
            userRepositoryMock.Mock.Setup(x => x.Add(It.IsAny<User>()));
            userRepositoryMock.Mock.Setup(x => x.UserNameExists(It.IsAny<string>())).ReturnsAsync(false);
            userRepositoryMock.Mock.Setup(x => x.EmailExists(It.IsAny<string>())).ReturnsAsync(true);


            var serviceUnderTest = ServiceProvider.GetService<IUserService>();

            // Act
            await serviceUnderTest!.Create(req);
        }

        [TestMethod]
        [ExpectedException(typeof(ConflictException))]
        public async Task Create_Should_Throw_When_UserNameExists()
        {
            // Arrange
            var Email = "test@domain.com";
            var userName = "Name";

            var req = new UserRequest
            {
                Email = Email,
                UserName = userName,
                FullName = "User Name",
                MobileNumber = "123",
                Language = "en",
                Culture = "en-US",
                Password = "pass"
            };

            var userRepositoryMock = ServiceProvider.GetService<IUserRepository>() as IMocked<IUserRepository>;
            userRepositoryMock!.Mock.SetupAllProperties();
            userRepositoryMock.Mock.Setup(x => x.Add(It.IsAny<User>()));
            userRepositoryMock.Mock.Setup(x => x.UserNameExists(It.IsAny<string>())).ReturnsAsync(true);
            userRepositoryMock.Mock.Setup(x => x.EmailExists(It.IsAny<string>())).ReturnsAsync(false);


            var serviceUnderTest = ServiceProvider.GetService<IUserService>();

            // Act
            await serviceUnderTest!.Create(req);
        }

        [TestMethod]
        public async Task GetById_Should_Return_User()
        {
            // Arrange
            var user = new User { Id = Guid.NewGuid(), UserName = "User" };
            var userReturn = new ReturnUserDto() { Id = Guid.NewGuid(), UserName = "User" };


            var userRepositoryMock = ServiceProvider.GetService<IUserRepository>() as IMocked<IUserRepository>;
            userRepositoryMock!.Mock.SetupAllProperties();
            userRepositoryMock.Mock.Setup(x => x.GetById(It.IsAny<Guid>())).ReturnsAsync(user);

            var helperMock = ServiceProvider.GetService<IMapper>() as IMocked<IMapper>;
            helperMock!.Mock.SetupAllProperties();
            helperMock.Mock.Setup(x => x.Map<ReturnUserDto>(It.IsAny<User>())).Returns(userReturn);

            var serviceUnderTest = ServiceProvider.GetService<IUserService>();

            
            // Act
            var result = await serviceUnderTest!.GetById(user.Id);

            Assert.IsNotNull(result);
            Assert.AreEqual(user.UserName, result.UserName);
        }

        [TestMethod]
        [ExpectedException(typeof(NotFoundException))]
        public async Task GetById_Should_Throw_When_NotFound()
        {
            // Arrange
            var userRepositoryMock = ServiceProvider.GetService<IUserRepository>() as IMocked<IUserRepository>;
            userRepositoryMock!.Mock.SetupAllProperties();
            userRepositoryMock.Mock.Setup(x => x.GetById(It.IsAny<Guid>())).ReturnsAsync((User)null!);


            var serviceUnderTest = ServiceProvider.GetService<IUserService>();

            // Act
            var result = await serviceUnderTest!.GetById(Guid.NewGuid());
        }

        [TestMethod]
        public async Task Update_Should_Update_User_Successfully()
        {
            var id = Guid.NewGuid();
            var existing = new User
            {
                Id = id,
                Email = "old@domain.com",
                UserName = "olduser"
            };
            var req = new UserRequest
            {
                Email = "new@domain.com",
                UserName = "newuser",
                FullName = "User",
                MobileNumber = "123",
                Language = "en",
                Culture = "en-US",
                Password = "newpass"
            };


            var userRepositoryMock = ServiceProvider.GetService<IUserRepository>() as IMocked<IUserRepository>;
            userRepositoryMock!.Mock.SetupAllProperties();
            userRepositoryMock.Mock.Setup(x => x.GetById(It.IsAny<Guid>())).ReturnsAsync(existing);
            userRepositoryMock.Mock.Setup(x => x.UserNameExists(It.IsAny<string>())).ReturnsAsync(false);
            userRepositoryMock.Mock.Setup(x => x.EmailExists(It.IsAny<string>())).ReturnsAsync(false);
            userRepositoryMock.Mock.Setup(x => x.Update(It.IsAny<User>())).ReturnsAsync(true);


            var expectedHash = new byte[] { 1, 2, 3 };
            var expectedSalt = new byte[] { 4, 5, 6 };

            var helperMock = ServiceProvider.GetService<IHelper>() as IMocked<IHelper>;
            helperMock!.Mock.SetupAllProperties();
            helperMock.Mock.Setup(x => x.Hash(It.IsAny<string>()))
                .Returns((expectedHash, expectedSalt));


            var serviceUnderTest = ServiceProvider.GetService<IUserService>();

            // Act
            var result = await serviceUnderTest!.Update(id, req);

            // Assert
            Assert.IsTrue(result);

            //serviceMock.Mock.Verify(mock => mock.GetDatabaseInfo(It.IsAny<ConnectionInfoDto>()), Times.Once);

            _repoMock.Verify(r => r.Update(It.Is<User>(u => u.Email == req.Email)), Times.Once);
        }


        // you got the point how i will write the rest of the tests for user functions and also for Client functions 
        // because lack of time i will not do that  
    }
}
