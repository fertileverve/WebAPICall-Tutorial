using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using APITest.App;
using APITest.APIDummyJSON;
using APITest.Dataverse;
using Microsoft.Xrm.Sdk;

namespace APITest.Tests
{
    public class ApplicationTests
    {
        private readonly Mock<IApiService> _mockApiService;
        private readonly Mock<IDataverseService> _mockDataverseService;
        private readonly Mock<ILogger<Application>> _mockLogger;

        private readonly Application _application;

        public ApplicationTests()
        {
            _mockApiService = new();
            _mockDataverseService = new();
            _mockLogger = new();

            _application = new Application(_mockApiService.Object, _mockDataverseService.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task RunAsync_ShouldCallAllServicesInOrder()
        {
            var fakeEmployees = new List<Employee>
            {
                new Employee { id = 1, firstName = "Test", lastName = "User" }
            };
            _mockApiService
                .Setup(api => api.GetEmployeesAsync(It.IsAny<string>()))
                .ReturnsAsync(fakeEmployees);

            var fakeGenderMap = new Dictionary<string, int> { { "male", 1 } };
            _mockDataverseService
                .Setup(dv => dv.GetChoiceMapAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(fakeGenderMap);

            var fakeSyncSummary = new SyncSummary { Created = 1 };
            _mockDataverseService
                .Setup(dv => dv.SyncEmployeesWithChangeTrackingAsync(It.IsAny<List<Entity>>()))
                .ReturnsAsync(fakeSyncSummary);

            await _application.RunAsync();

            _mockApiService.Verify(api => api.GetEmployeesAsync("users"), Times.Once);
            _mockDataverseService.Verify(dv => dv.GetChoiceMapAsync("crfbe_employee", "crfbe_gender"), Times.Once);
            _mockDataverseService.Verify(dv => dv.SyncEmployeesWithChangeTrackingAsync(It.IsAny<List<Entity>>()), Times.Once);
        }

        [Fact]
        public async Task RunAsync_WhenApiReturnsEmptyList_ShouldStillSyncAndLog()
        {
            _mockApiService
                .Setup(api => api.GetEmployeesAsync(It.IsAny<string>()))
                .ReturnsAsync(new List<Employee>());

            _mockDataverseService
                .Setup(dv => dv.GetChoiceMapAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new Dictionary<string, int>());

            _mockDataverseService
                .Setup(dv => dv.SyncEmployeesWithChangeTrackingAsync(It.IsAny<List<Entity>>()))
                .ReturnsAsync(new SyncSummary());

            await _application.RunAsync();

            _mockApiService.Verify(api => api.GetEmployeesAsync("users"), Times.Once);
            _mockDataverseService.Verify(dv => dv.SyncEmployeesWithChangeTrackingAsync(It.Is<List<Entity>>(entities => entities.Count == 0)), Times.Once);
        }

        [Fact]
        public async Task RunAsync_WhenApiThrowsException_ShouldLogError()
        {
            _mockApiService
                .Setup(api => api.GetEmployeesAsync(It.IsAny<string>()))
                .ThrowsAsync(new System.Exception("API failure"));

            await Assert.ThrowsAsync<System.Exception>(() => _application.RunAsync());

            _mockLogger.Verify(logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Application failed")),
                It.IsAny<System.Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }
    }
}