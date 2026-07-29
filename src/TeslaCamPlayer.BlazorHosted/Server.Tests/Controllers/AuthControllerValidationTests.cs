using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using TeslaCamPlayer.BlazorHosted.Shared.Models;
using Xunit;

namespace TeslaCamPlayer.BlazorHosted.Server.Tests.Controllers
{
    public class AuthControllerValidationTests
    {
        [Fact]
        public void LoginRequest_ShouldBeInvalid_WhenUsernameIsTooLong()
        {
            // Arrange
            var request = new LoginRequest
            {
                Username = new string('a', 51),
                Password = "ValidPassword"
            };

            // Act
            var validationResults = ValidateModel(request);

            // Assert
            Assert.Contains(validationResults, v => v.MemberNames.Contains("Username"));
            Assert.Contains(validationResults, v => v.ErrorMessage.Contains("too long"));
        }

        [Fact]
        public void LoginRequest_ShouldBeInvalid_WhenPasswordIsTooLong()
        {
            // Arrange
            var request = new LoginRequest
            {
                Username = "ValidUser",
                Password = new string('a', 101)
            };

            // Act
            var validationResults = ValidateModel(request);

            // Assert
            Assert.Contains(validationResults, v => v.MemberNames.Contains("Password"));
            Assert.Contains(validationResults, v => v.ErrorMessage.Contains("too long"));
        }

        [Fact]
        public void LoginRequest_ShouldBeValid_WhenFieldsAreWithinLimits()
        {
            // Arrange
            var request = new LoginRequest
            {
                Username = new string('a', 50),
                Password = new string('a', 100)
            };

            // Act
            var validationResults = ValidateModel(request);

            // Assert
            Assert.Empty(validationResults);
        }

        [Fact]
        public void UpdateAuthRequest_ShouldBeInvalid_WhenFieldsAreTooLong()
        {
            // Arrange
            var request = new UpdateAuthRequest
            {
                Username = new string('a', 51),
                Password = new string('a', 101),
                CurrentPassword = new string('a', 101),
                FirstName = new string('a', 101)
            };

            // Act
            var validationResults = ValidateModel(request);

            // Assert
            Assert.Contains(validationResults, v => v.MemberNames.Contains("Username"));
            Assert.Contains(validationResults, v => v.MemberNames.Contains("Password"));
            Assert.Contains(validationResults, v => v.MemberNames.Contains("CurrentPassword"));
            Assert.Contains(validationResults, v => v.MemberNames.Contains("FirstName"));
        }

        private IList<ValidationResult> ValidateModel(object model)
        {
            var validationResults = new List<ValidationResult>();
            var ctx = new ValidationContext(model, null, null);
            Validator.TryValidateObject(model, ctx, validationResults, true);
            return validationResults;
        }
    }
}
