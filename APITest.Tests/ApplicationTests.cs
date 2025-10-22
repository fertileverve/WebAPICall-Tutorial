using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using APITest.App;
using APITest.APIDummyJSON;
using APITest.Dataverse;
using System.Collections.Generic;
using System.Threading.Tasks;
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
    }
}