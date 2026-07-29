using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using TeslaCamPlayer.BlazorHosted.Shared.Models;
using Xunit;

namespace TeslaCamPlayer.BlazorHosted.Server.Tests.Models
{
    public class ValidationTests
    {
        [Fact]
        public void UpdateAuthRequest_ShouldBeInvalid_WhenPasswordIsShort()
        {
            var model = new UpdateAuthRequest
            {
                Username = "Admin",
                Password = "short", // < 8 chars
                IsEnabled = true
            };

            var context = new ValidationContext(model);
            var results = new List<ValidationResult>();
            var isValid = Validator.TryValidateObject(model, context, results, true);

            Assert.False(isValid);
            Assert.Contains(results, r => r.ErrorMessage == "Password must be at least 8 characters.");
        }

        [Fact]
        public void UpdateAuthRequest_ShouldBeValid_WhenPasswordIsStrong()
        {
            var model = new UpdateAuthRequest
            {
                Username = "Admin",
                Password = "StrongPassword123", // > 8 chars
                IsEnabled = true
            };

            var context = new ValidationContext(model);
            var results = new List<ValidationResult>();
            var isValid = Validator.TryValidateObject(model, context, results, true);

            Assert.True(isValid);
        }

        [Fact]
        public void UpdateAuthRequest_ShouldBeInvalid_WhenUsernameIsTooShort()
        {
            var model = new UpdateAuthRequest
            {
                Username = "Ad", // < 3 chars
                IsEnabled = true
            };

            var context = new ValidationContext(model);
            var results = new List<ValidationResult>();
            var isValid = Validator.TryValidateObject(model, context, results, true);

            Assert.False(isValid);
            Assert.Contains(results, r => r.MemberNames.Contains("Username"));
        }

        [Fact]
        public void LoginRequest_ShouldBeInvalid_WhenUsernameIsMissing()
        {
             var model = new LoginRequest
             {
                 Password = "SomePassword"
             };

             var context = new ValidationContext(model);
             var results = new List<ValidationResult>();
             var isValid = Validator.TryValidateObject(model, context, results, true);

             Assert.False(isValid);
             Assert.Contains(results, r => r.MemberNames.Contains("Username"));
        }
    }
}
