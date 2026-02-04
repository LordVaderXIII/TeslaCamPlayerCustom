using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TeslaCamPlayer.BlazorHosted.Server.Controllers;
using TeslaCamPlayer.BlazorHosted.Server.Data;
using TeslaCamPlayer.BlazorHosted.Shared.Models;
using Xunit;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using TeslaCamPlayer.BlazorHosted.Server.Services.Interfaces;
using Moq;

namespace TeslaCamPlayer.BlazorHosted.Server.Tests.Controllers
{
    public class AuthControllerTests
    {
        private DbContextOptions<TeslaCamDbContext> _dbContextOptions;
        private Mock<IAuthSetupService> _mockAuthSetupService;

        public AuthControllerTests()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            _dbContextOptions = new DbContextOptionsBuilder<TeslaCamDbContext>()
                .UseSqlite(connection)
                .Options;

            using var context = new TeslaCamDbContext(_dbContextOptions);
            context.Database.EnsureCreated();
            _mockAuthSetupService = new Mock<IAuthSetupService>();
            _mockAuthSetupService.Setup(s => s.SetupToken).Returns("ValidToken");
        }

        [Fact]
        public async Task Update_ShouldReturnUnauthorized_WhenAuthDisabledButPasswordExists_AndCurrentPasswordMissing()
        {
            // Arrange
            using var context = new TeslaCamDbContext(_dbContextOptions);
            var hasher = new PasswordHasher<User>();
            var user = new User
            {
                Id = "Admin",
                Username = "Admin",
                IsEnabled = false, // Auth disabled
                PasswordHash = hasher.HashPassword(null, "ExistingPassword")
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var controller = new AuthController(context, _mockAuthSetupService.Object);
            // Simulate unauthenticated user
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            var request = new UpdateAuthRequest
            {
                IsEnabled = true,
                Username = "Admin",
                Password = "NewPassword",
                CurrentPassword = null // Missing!
            };

            // Act
            var result = await controller.Update(request);

            // Assert
            Assert.IsType<UnauthorizedObjectResult>(result);
            var unauthorizedResult = result as UnauthorizedObjectResult;
            Assert.Equal("Current password is required to change settings.", unauthorizedResult.Value);
        }

        [Fact]
        public async Task Update_ShouldReturnUnauthorized_WhenAuthDisabledButPasswordExists_AndCurrentPasswordIncorrect()
        {
            // Arrange
            using var context = new TeslaCamDbContext(_dbContextOptions);
            var hasher = new PasswordHasher<User>();
            var user = new User
            {
                Id = "Admin",
                Username = "Admin",
                IsEnabled = false,
                PasswordHash = hasher.HashPassword(null, "ExistingPassword")
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var controller = new AuthController(context, _mockAuthSetupService.Object);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            var request = new UpdateAuthRequest
            {
                IsEnabled = true,
                Username = "Admin",
                Password = "NewPassword",
                CurrentPassword = "WrongPassword"
            };

            // Act
            var result = await controller.Update(request);

            // Assert
            Assert.IsType<UnauthorizedObjectResult>(result);
            var unauthorizedResult = result as UnauthorizedObjectResult;
            Assert.Equal("Invalid current password.", unauthorizedResult.Value);
        }

        [Fact]
        public async Task Update_ShouldSucceed_WhenAuthDisabledButPasswordExists_AndCurrentPasswordCorrect()
        {
            // Arrange
            using var context = new TeslaCamDbContext(_dbContextOptions);
            var hasher = new PasswordHasher<User>();
            var user = new User
            {
                Id = "Admin",
                Username = "Admin",
                IsEnabled = false,
                PasswordHash = hasher.HashPassword(null, "ExistingPassword")
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var controller = new AuthController(context, _mockAuthSetupService.Object);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            var request = new UpdateAuthRequest
            {
                IsEnabled = true,
                Username = "Admin",
                Password = "NewPassword",
                CurrentPassword = "ExistingPassword"
            };

            // Act
            var result = await controller.Update(request);

            // Assert
            Assert.IsType<OkResult>(result);

            var updatedUser = await context.Users.FindAsync("Admin");
            Assert.True(updatedUser.IsEnabled);
            var verification = hasher.VerifyHashedPassword(updatedUser, updatedUser.PasswordHash, "NewPassword");
            Assert.Equal(PasswordVerificationResult.Success, verification);
        }

        [Fact]
        public async Task Update_ShouldFail_WhenAuthDisabledAndPasswordMissing_AndTokenMissing()
        {
            // Arrange
            using var context = new TeslaCamDbContext(_dbContextOptions);
            var user = new User
            {
                Id = "Admin",
                Username = "Admin",
                IsEnabled = false,
                PasswordHash = null // Initial setup
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var controller = new AuthController(context, _mockAuthSetupService.Object);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            var request = new UpdateAuthRequest
            {
                IsEnabled = true,
                Username = "Admin",
                Password = "NewPassword",
                CurrentPassword = null,
                SetupToken = null // Missing
            };

            // Act
            var result = await controller.Update(request);

            // Assert
            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task Update_ShouldFail_WhenAuthDisabledAndPasswordMissing_AndTokenInvalid()
        {
            // Arrange
            using var context = new TeslaCamDbContext(_dbContextOptions);
            var user = new User
            {
                Id = "Admin",
                Username = "Admin",
                IsEnabled = false,
                PasswordHash = null // Initial setup
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var controller = new AuthController(context, _mockAuthSetupService.Object);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            var request = new UpdateAuthRequest
            {
                IsEnabled = true,
                Username = "Admin",
                Password = "NewPassword",
                CurrentPassword = null,
                SetupToken = "InvalidToken"
            };

            // Act
            var result = await controller.Update(request);

            // Assert
            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task Update_ShouldSucceed_WhenAuthDisabledAndPasswordMissing_AndTokenCorrect()
        {
            // Arrange
            using var context = new TeslaCamDbContext(_dbContextOptions);
            var user = new User
            {
                Id = "Admin",
                Username = "Admin",
                IsEnabled = false,
                PasswordHash = null // Initial setup
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var controller = new AuthController(context, _mockAuthSetupService.Object);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            var request = new UpdateAuthRequest
            {
                IsEnabled = true,
                Username = "Admin",
                Password = "NewPassword",
                CurrentPassword = null,
                SetupToken = "ValidToken"
            };

            // Act
            var result = await controller.Update(request);

            // Assert
            Assert.IsType<OkResult>(result);

            var updatedUser = await context.Users.FindAsync("Admin");
            Assert.True(updatedUser.IsEnabled);
            Assert.NotNull(updatedUser.PasswordHash);
        }
    }
}
