using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using APITest.Validator;

namespace APITest.Tests
{
    public class HostValidatorTests
    {
        private readonly Mock<ILogger<HostValidator>> _mockLogger;
        private readonly HostValidator _validator;

        public HostValidatorTests()
        {
            _mockLogger = new Mock<ILogger<HostValidator>>();
            _validator = new HostValidator(_mockLogger.Object);
        }

        [Fact]
        public void IsHOstAllowed_WithVAlidURL_ReturnsTrue()
        {
            var url = "https://dummyjson.com/users?limit=0";
            var allowedHosts = new List<string> { "dummyjson.com" };

            var result = _validator.IsHostAllowed(url, allowedHosts);

            Assert.True(result);
        }

        [Fact]
        public void IsHostAllowed_WithDisallowedUrl_ReturnsFalse()
        {
            var url = "https://google.com/users";
            var allowedHosts = new List<string> { "dummyjson.com" };

            var result = _validator.IsHostAllowed(url, allowedHosts);

            Assert.False(result);
        }

        [Fact]
        public void IsHostAllowed_WithNonHttpsUrl_ReturnsFalse()
        {
            var url = "http://dummyjson.com/users?limit=0";
            var allowedHosts = new List<string> { "dummyjson.com" };

            var result = _validator.IsHostAllowed(url, allowedHosts);

            Assert.False(result);
        }

        [Fact]
        public void ValidateHost_WithDisallowedUrl_ThrowsSecurityException()
        {
            var url = "https://evil.com";
            var allowedHosts = new List<string> { "dummyjson.com" };
            var serviceName = "TestService";

            Assert.Throws<SecurityException>(() =>
            {
                _validator.ValidateHost(url, allowedHosts, serviceName);
            });
        }

        [Fact]
        public void IsHostAllowed_WithNullUrl_ReturnsFalse()
        {
            var allowedHosts = new List<string> { "dummyjson.com" };
            Assert.False(_validator.IsHostAllowed(null, allowedHosts));
        }

        [Fact]
        public void IsHostAllowed_WithEmptyAllowedHosts_ReturnsFalse()
        {
            var url = "https://dummyjson.com/users?limit=0";
            var allowedHosts = new List<string>();
            Assert.False(_validator.IsHostAllowed(url, allowedHosts));
        }

        [Fact]
        public void IsHostAllowed_WithMalformedUrl_ReturnsFalse()
        {
            var url = "not-a-real-url";
            var allowedHosts = new List<string> { "dummyjson.com" };
            Assert.False(_validator.IsHostAllowed(url, allowedHosts));
        }

        [Fact]
        public void IsHostAllowed_WithWildcardHost_MatchesSubdomain()
        {
            var url = "https://api.dummyjson.com/users?limit=0";
            var allowedHosts = new List<string> { "*.dummyjson.com" };
            Assert.True(_validator.IsHostAllowed(url, allowedHosts));
        }

        [Fact]
        public void IsHoseAllowed_WithCaseInsensitiveHostMatch_ReturnsTrue()
        {
            var url = "https://DUMMYJSON.COM/users?limit=0";
            var allowedHosts = new List<string> { "dummyjson.com" };
            Assert.True(_validator.IsHostAllowed(url, allowedHosts));
        }
    }
}