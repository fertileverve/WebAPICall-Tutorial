using APITest.Validator;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace APITest.Dataverse
{
    #region Dataverse Service Factory
    public interface IDataverseServiceFactory
    {
        ServiceClient CreateServiceClient();
    }

    public class DataverseServiceFactory : IDataverseServiceFactory
    {
        private readonly DataverseSettings _settings;
        private readonly IHostValidator _hostValidator;
        private readonly ILogger<DataverseServiceFactory> _logger;

        public DataverseServiceFactory(
            DataverseSettings settings,
            IHostValidator hostValidator,
            ILogger<DataverseServiceFactory> logger
        )
        {
            _settings = settings;
            _hostValidator = hostValidator;
            _logger = logger;
        }

        public ServiceClient CreateServiceClient()
        {
            _hostValidator.ValidateHost(_settings.Uri, _settings.AllowedHosts, "Dataverse");
            _logger.LogInformation("Creating Dataverse ServiceClient for {Uri}", _settings.Uri);

            string connectionString = $@"AuthType = ClientSecret;
                SkipDiscovery = true;
                Url = {_settings.Uri};
                ClientId = {_settings.ClientId};
                ClientSecret = {_settings.ClientSecret};
                RequireNewInstance=true";

            var serviceClient = new ServiceClient(connectionString);

            if (serviceClient == null || !serviceClient.IsReady)
            {
                var error = serviceClient?.LastError ?? "Unknown error";
                _logger.LogError("Failed to connect to Dataverse: {Error}", error);
                throw new InvalidOperationException($"Cannot connect to Dataverse: {error}");
            }

            _logger.LogInformation("Successfully connected to Dataverse");
            return serviceClient;
        }
    }
    #endregion

    #region Dataverse Service
    public interface IDataverseService
    {
        Task CreateEmployeesAsync(List<Entity> entities);
        Task<Dictionary<string, int>> GetChoiceMapAsync(string entityName, string attributeName);
        Task TestConnectionAsync();
    }

    public class DataverseService : IDataverseService, IDisposable
    {
        private readonly IDataverseServiceFactory _serviceFactory;
        private readonly ILogger<DataverseService> _logger;
        private ServiceClient? _serviceClient;
        private bool _disposed;

        public DataverseService(
            IDataverseServiceFactory serviceFactory,
            ILogger<DataverseService> logger
        )
        {
            _serviceFactory = serviceFactory;
            _logger = logger;
        }

        private ServiceClient GetServiceClient()
        {
            if (_serviceClient == null || !_serviceClient.IsReady)
            {
                _serviceClient?.Dispose();
                _serviceClient = _serviceFactory.CreateServiceClient();
            }
            return _serviceClient;
        }

        public async Task CreateEmployeesAsync(List<Entity> entities)
        {
            if (entities == null || entities.Count == 0)
            {
                throw new ArgumentException("Entity list cannot be null or empty", nameof(entities));
            }

            try
            {
                var client = GetServiceClient();
                _logger.LogInformation("Creating {Count} employee records in Dataverse", entities.Count);

                const int batchSize = 100;
                for (int i = 0; i < entities.Count; i += batchSize)
                {
                    var batch = entities.Skip(i).Take(batchSize).ToList();

                    var employeeCollection = new EntityCollection(batch)
                    {
                        EntityName = entities[0].LogicalName
                    };

                    var createMultipleRequest = new CreateMultipleRequest
                    {
                        Targets = employeeCollection
                    };

                    await Task.Run(() => client.Execute(createMultipleRequest));

                    _logger.LogInformation("Created batch {Current}/{Total}", Math.Min(i + batchSize, entities.Count), entities.Count);
                }

                _logger.LogInformation("Successfully created all Employee records");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create employee records in Dataverse");
                throw;
            }
        }

        public async Task<Dictionary<string, int>> GetChoiceMapAsync(string entityName, string attributeName)
        {
            Dictionary<string, int> choiceMap = new();

            try
            {
                var client = GetServiceClient();

                var retrieveAttributeRequest = new RetrieveAttributeRequest
                {
                    EntityLogicalName = entityName,
                    LogicalName = attributeName,
                    MetadataId = Guid.Empty,
                    RetrieveAsIfPublished = true
                };

                var response = await Task.Run(() => (RetrieveAttributeResponse)client.Execute(retrieveAttributeRequest));

                if (response.AttributeMetadata is EnumAttributeMetadata choiceMetadata)
                {
                    foreach (var option in choiceMetadata.OptionSet.Options)
                    {
                        string label = option.Label.UserLocalizedLabel.Label.ToLower();
                        int value = option.Value ?? 0;
                        choiceMap.Add(label, value);
                    }

                    _logger.LogInformation("Retrieved {Count} choice options for {Entity}.{Attribute}", choiceMap.Count, entityName, attributeName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to trieve choice map for {Entity}.{Attribute}", entityName, attributeName);
                throw;
            }

            return choiceMap;
        }

        public async Task TestConnectionAsync()
        {
            try
            {
                var client = GetServiceClient();

                var query = new QueryExpression("account")
                {
                    ColumnSet = new ColumnSet("name"),
                    TopCount = 5
                };

                var accounts = await Task.Run(() => client.RetrieveMultiple(query));

                _logger.LogInformation("Test connection successful. Found {Count} accounts",
                    accounts.Entities.Count);

                foreach (Entity account in accounts.Entities)
                {
                    Console.WriteLine($"Account: {account.GetAttributeValue<string>("name")}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Test connection failed");
                throw;
            }
        }

        public void ShowEntitiesAsync(List<Entity> entities)
        {
            foreach (Entity entity in entities)
            {
                Console.WriteLine($"Entity ID: {entity["crfbe_name"]}");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _serviceClient?.Dispose();
                _disposed = true;
            }
        }
    }
    #endregion
}