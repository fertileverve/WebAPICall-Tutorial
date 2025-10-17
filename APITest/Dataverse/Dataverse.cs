using APITest.Validator;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using Serilog;
using System.Text;

namespace APITest.Dataverse
{
    #region Change Tracking Models
    /// <summary>
    /// Represents a change to a single field
    /// </summary>
    public class FieldChange
    {
        public string FieldName { get; set; } = "";
        public object? OldValue { get; set; }
        public object? NewValue { get; set; }
        public string FieldType { get; set; } = "";
    }

    /// <summary>
    /// Represents all changes for one employee record
    /// </summary>
    public class EmployeeChangeRecord
    {
        public string EmployeeId { get; set; } = "";
        public string EmployeeName { get; set; } = "";
        public DateTime SyncDateTime { get; set; }
        public List<FieldChange> Changes { get; set; } = new();
        public bool IsNewRecord { get; set; }
    }

    /// <summary>
    /// Summary of sync operation
    /// </summary>
    public class SyncSummary
    {
        public int TotalRecords { get; set; }
        public int Created { get; set; }
        public int Updated { get; set; }
        public int Unchanged { get; set; }
        public int Failed { get; set; }
        public int TotalFieldChanges { get; set; }
        public DateTime SyncStartTime { get; set; }
        public DateTime SyncEndTime { get; set; }
        public TimeSpan Duration => SyncEndTime - SyncStartTime;
    }
    #endregion

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
        // Task CreateEmployeesAsync(List<Entity> entities);
        Task<Dictionary<string, int>> GetChoiceMapAsync(string entityName, string attributeName);
        Task TestConnectionAsync();
        Task SyncEmployeesAsync(List<Entity> entities);
        Task<SyncSummary> SyncEmployeesWithChangeTrackingAsync(List<Entity> entities);
    }

    public class DataverseService : IDataverseService, IDisposable
    {
        private readonly IDataverseServiceFactory _serviceFactory;
        private readonly ILogger<DataverseService> _logger;
        private ServiceClient? _serviceClient;
        private bool _disposed;
        private readonly Serilog.ILogger _auditLogger;

        public DataverseService(
            IDataverseServiceFactory serviceFactory,
            ILogger<DataverseService> logger
        )
        {
            _serviceFactory = serviceFactory;
            _logger = logger;

            _auditLogger = new LoggerConfiguration()
                .WriteTo.File(
                    path: "logs/audit/employee-changes-.log",
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level}] {Message:lj}{NewLine}{Exception}",
                    retainedFileCountLimit: 30)
                .WriteTo.File(
                    path: "logs/audit/employee-changes-.json",
                    rollingInterval: RollingInterval.Day,
                    formatter: new Serilog.Formatting.Json.JsonFormatter(),
                    retainedFileCountLimit: 30)
                .CreateLogger();
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

        /// <summary>
        /// Syncs employees with full change tracking and audit logging
        /// </summary>
        public async Task<SyncSummary> SyncEmployeesWithChangeTrackingAsync(List<Entity> entities)
        {
            if (entities == null || entities.Count == 0)
            {
                throw new ArgumentException("Entity list cannot be null or empty", nameof(entities));
            }

            var summary = new SyncSummary
            {
                TotalRecords = entities.Count,
                SyncStartTime = DateTime.UtcNow
            };

            try
            {
                var client = GetServiceClient();

                _logger.LogInformation("Starting sync with chnage tracking for {Count} employees", entities.Count);

                var existingEmployees = await GetExistingEmployeesWithDataAsync();

                foreach (var newEntity in entities)
                {
                    try
                    {
                        string apiEmployeeId = newEntity.KeyAttributes["crfbe_id"]?.ToString() ?? string.Empty;

                        if (string.IsNullOrEmpty(apiEmployeeId))
                        {
                            _logger.LogWarning("Employee missing API ID, skipping");
                            summary.Failed++;
                            continue;
                        }

                        if (existingEmployees.TryGetValue(apiEmployeeId, out Entity? existingEntity))
                        {
                            var changeRecord = CompareEntities(existingEntity, newEntity, apiEmployeeId);

                            if (changeRecord.Changes.Count > 0)
                            {
                                newEntity.Id = existingEntity.Id;
                                await Task.Run(() => client.Update(newEntity));

                                summary.Updated++;
                                summary.TotalFieldChanges += changeRecord.Changes.Count;

                                LogEmployeeChanges(changeRecord);

                                _logger.LogInformation("Update employee {Name} ({Id}) - {ChangeCount} fields(s) changed",
                                    changeRecord.EmployeeName,
                                    apiEmployeeId,
                                    changeRecord.Changes.Count
                                );
                            }
                            else
                            {
                                summary.Unchanged++;
                                _logger.LogDebug("No changes for employee {Name} ({Id})",
                                    newEntity["crfbe_name"],
                                    apiEmployeeId
                                );
                            }
                        }
                        else
                        {
                            // Finding an issue with the key attribute when creating the entity
                            // Attempting to remove the key attribute and and it back in as just a value
                            var crfbe_id = newEntity.KeyAttributes["crfbe_id"]?.ToString() ?? string.Empty;
                            newEntity.KeyAttributes.Remove("crfbe_id");
                            newEntity["crfbe_id"] = crfbe_id;

                            var newId = await Task.Run(() => client.Create(newEntity));
                            summary.Created++;

                            var changeRecord = new EmployeeChangeRecord
                            {
                                EmployeeId = apiEmployeeId,
                                EmployeeName = newEntity.GetAttributeValue<string>("crfbe_name") ?? string.Empty,
                                SyncDateTime = DateTime.UtcNow,
                                IsNewRecord = true
                            };

                            LogEmployeeChanges(changeRecord);

                            _logger.LogInformation("Created new employee {Name} ({Id})",
                                changeRecord.EmployeeName,
                                apiEmployeeId
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        summary.Failed++;
                        string empName = newEntity.GetAttributeValue<string>("crfbe_name") ?? string.Empty;
                        _logger.LogError(ex, "Failed to sync employee: {Name}", empName);
                    }
                }

                summary.SyncEndTime = DateTime.UtcNow;

                LogSyncSummary(summary);

                _logger.LogInformation(
                    "Suync Complete. Created: {Created}, Updated: {Updated}, Unchanged: {Unchanged}, Failed: {Failed}, Total Changes: {TotalChanges}, Duration: {Duration}s",
                    summary.Created,
                    summary.Updated,
                    summary.Unchanged,
                    summary.Failed,
                    summary.TotalFieldChanges,
                    summary.Duration.TotalSeconds
                );

                return summary;
            }
            catch (Exception ex)
            {
                summary.SyncEndTime = DateTime.UtcNow;
                _logger.LogError(ex, "Failed to Sync employee records");
                throw;
            }
        }

        /// <summary>
        /// Retrieves all existing employees with thier full data for comparison
        /// </summary>
        public async Task<Dictionary<string, Entity>> GetExistingEmployeesWithDataAsync()
        {
            var client = GetServiceClient();
            var existingEmployees = new Dictionary<string, Entity>();

            var query = new QueryExpression("crfbe_employee")
            {
                ColumnSet = new ColumnSet(
                    "crfbe_id",
                    "crfbe_name",
                    "crfbe_firstname",
                    "crfbe_lastname",
                    "crfbe_age",
                    "crfbe_gender",
                    "crfbe_email",
                    "crfbe_workphone",
                    "crfbe_username",
                    "crfbe_password",
                    "crfbe_birithdate",
                    "crfbe_image",
                    "crfbe_address1",
                    "crfbe_city",
                    "crfbe_statecode",
                    "crfbe_zip",
                    "crfbe_latitude",
                    "crfbe_longitude"
                ),
                PageInfo = new PagingInfo
                {
                    Count = 5000,
                    PageNumber = 1
                }
            };

            EntityCollection results;
            do
            {
                results = await Task.Run(() => client.RetrieveMultiple(query));

                foreach (var entity in results.Entities)
                {
                    if (entity.Contains("crfbe_id"))
                    {
                        //string apiId = entity["crfbe_id"].ToString();
                        string apiId = entity.GetAttributeValue<string>("crfbe_id") ?? string.Empty;
                        existingEmployees[apiId] = entity;
                    }
                }

                query.PageInfo.PageNumber++;
                query.PageInfo.PagingCookie = results.PagingCookie;

            } while (results.MoreRecords);

            _logger.LogInformation("Retrieved {Count} existing employees for comparison",
                existingEmployees.Count);

            return existingEmployees;
        }

        /// <summary>
        /// Compares old and new entity values and returns detected changes
        /// </summary>
        private EmployeeChangeRecord CompareEntities(Entity oldEntity, Entity newEntity, string employeeId)
        {
            var changeRecord = new EmployeeChangeRecord
            {
                EmployeeId = employeeId,
                EmployeeName = newEntity.GetAttributeValue<string>("crfbe_name") ?? string.Empty,
                SyncDateTime = DateTime.UtcNow,
                IsNewRecord = false
            };

            var fieldsToCompare = new[]
            {
                "crfbe_id",
                "crfbe_name",
                "crfbe_firstname",
                "crfbe_lastname",
                "crfbe_age",
                "crfbe_gender",
                "crfbe_email",
                "crfbe_workphone",
                "crfbe_username",
                "crfbe_password",
                "crfbe_birithdate",
                "crfbe_image",
                "crfbe_address1",
                "crfbe_city",
                "crfbe_statecode",
                "crfbe_zip",
                "crfbe_latitude",
                "crfbe_longitude"
            };

            foreach (var fieldName in fieldsToCompare)
            {
                if (!newEntity.Contains(fieldName))
                    continue;

                object? newValue = newEntity[fieldName];
                object? oldValue = oldEntity.Contains(fieldName) ? oldEntity[fieldName] : null;

                if (!ValuesAreEqual(oldValue, newValue))
                {
                    changeRecord.Changes.Add(new FieldChange
                    {
                        FieldName = fieldName,
                        OldValue = FormatValueForLogging(oldValue),
                        NewValue = FormatValueForLogging(newValue),
                        FieldType = newValue?.GetType().Name ?? "null"
                    });
                }
            }

            return changeRecord;
        }

        /// <summary>
        /// Compares two values considering thier types
        /// </summary>
        private bool ValuesAreEqual(object? oldValue, object? newValue)
        {
            if (oldValue == null && newValue == null)
                return true;

            if (oldValue == null || newValue == null)
                return false;

            if (oldValue is OptionSetValue oldOption && newValue is OptionSetValue newOption)
                return oldOption.Value == newOption.Value;

            if (oldValue is DateTime oldDate && newValue is DateTime newDate)
                return oldDate.Date == newDate.Date;

            return oldValue.Equals(newValue);
        }

        /// <summary>
        /// Formats values for human-readable logging
        /// </summary>
        private object? FormatValueForLogging(object? value)
        {
            if (value == null)
                return null;

            if (value is OptionSetValue optionSet)
                return optionSet.Value;

            if (value is DateTime dateTime)
                return dateTime.ToString("yyyy-MM-dd");

            return value;
        }

        /// <summary>
        /// Logs employee changes to audit file
        /// </summary>
        private void LogEmployeeChanges(EmployeeChangeRecord changeRecord)
        {
            if (changeRecord.IsNewRecord)
            {
                _auditLogger.Information(
                    "NEW EMPLOYEE CREATED | ID: {EmployeeId} | Name: {EmployeeName} | DateTime: {DateTime}",
                    changeRecord.EmployeeId,
                    changeRecord.EmployeeName,
                    changeRecord.SyncDateTime
                );
            }
            else if (changeRecord.Changes.Count > 0)
            {
                var changeDetails = new StringBuilder();
                changeDetails.AppendLine($"EMPPLOYEE UPDATED | ID: {changeRecord.EmployeeId} | Name: {changeRecord.EmployeeName}");

                foreach (var change in changeRecord.Changes)
                {
                    changeDetails.AppendLine($"  • {change.FieldName}: [{change.OldValue ?? "null"}] → [{change.NewValue ?? "null"}]");
                }

                _auditLogger.Information(changeDetails.ToString());

                _auditLogger.Information(
                    "Employee {EmployeeId} update with {ChangeCount} changes: {@Changes}",
                    changeRecord.EmployeeId,
                    changeRecord.Changes.Count,
                    changeRecord.Changes
                );
            }
        }

        /// <summary>
        /// Logs sync summary
        /// </summary>
        private void LogSyncSummary(SyncSummary summary)
        {
            _auditLogger.Information(
                "SYNC SUMMARY | Total: {Total} | Created: {Created} | Updated: {Updated} | Unchanged: {Unchanged} | Failed: {Failed} | Total Changes {TotalChanges} | Duration: {Duration}s",
                summary.TotalRecords,
                summary.Created,
                summary.Updated,
                summary.Unchanged,
                summary.Failed,
                summary.TotalFieldChanges,
                summary.Duration.TotalSeconds
            );

            _auditLogger.Information("Sync summary: {@Summary}", summary);
        }

        /// <summary>
        /// Syncs employees using Upsert - creates new records or updates existing ones
        /// based on an alternate key ID from the API and Dataverse
        /// </summary>
        public async Task SyncEmployeesAsync(List<Entity> entities)
        {
            if (entities == null || entities.Count == 0)
            {
                throw new ArgumentException("Entity list cannot be null or empty", nameof(entities));
            }

            try
            {
                var client = GetServiceClient();

                _logger.LogInformation("Syncing {Count} employee records using Upsert", entities.Count);

                int created = 0;
                int updated = 0;
                int failed = 0;
                const int batchSize = 50;

                for (int i = 0; i < entities.Count; i += batchSize)
                {
                    var batch = entities.Skip(i).Take(batchSize).ToList();

                    foreach (var entity in batch)
                    {
                        try
                        {
                            var upsertRequest = new UpsertRequest
                            {
                                Target = entity
                            };

                            var upsertResponse = await Task.Run(() =>
                                (UpsertResponse)client.Execute(upsertRequest));

                            if (upsertResponse.RecordCreated)
                            {
                                created++;
                                _logger.LogDebug("Created employee: {Name}", entity["crfbe_name"]);
                            }
                            else
                            {
                                updated++;
                                _logger.LogDebug("Updated employee {Name}", entity["crfbe_name"]);
                            }
                        }
                        catch (Exception ex)
                        {
                            failed++;
                            _logger.LogError(ex, "Failed to upsert employee: {Name}",
                                entity.Contains("crfbe_name") ? entity["crfbe_name"] : "Unknown");
                        }
                    }

                    _logger.LogInformation("Processed batch {Current}/{Total}",
                        Math.Min(i + batchSize, entities.Count), entities.Count);
                }

                _logger.LogInformation("Sync complete. Created: {Created}, Updated: {Updated}, Failed: {Failed}",
                    created, updated, failed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync employee records");
                throw;
            }
        }

        /// <summary>
        /// Upserts a single employee record
        /// </summary>
        public async Task UpserEmployeeAsync(Entity entity, string alternateKey)
        {
            try
            {
                var client = GetServiceClient();

                var upsertRequest = new UpsertRequest
                {
                    Target = entity
                };

                var upsertResponse = await Task.Run(() =>
                    (UpsertResponse)client.Execute(upsertRequest));

                if (upsertResponse.RecordCreated)
                {
                    _logger.LogInformation("Created new employee record with ID: {Id}",
                        upsertResponse.Target.Id);
                }
                else
                {
                    _logger.LogInformation("Updated existing employee record with ID: {Id}",
                        upsertResponse.Target.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upsert employee");
                throw;
            }
        }

        // public async Task CreateEmployeesAsync(List<Entity> entities)
        // {
        //     if (entities == null || entities.Count == 0)
        //     {
        //         throw new ArgumentException("Entity list cannot be null or empty", nameof(entities));
        //     }

        //     try
        //     {
        //         var client = GetServiceClient();
        //         _logger.LogInformation("Creating {Count} employee records in Dataverse", entities.Count);

        //         const int batchSize = 100;
        //         for (int i = 0; i < entities.Count; i += batchSize)
        //         {
        //             var batch = entities.Skip(i).Take(batchSize).ToList();

        //             var employeeCollection = new EntityCollection(batch)
        //             {
        //                 EntityName = entities[0].LogicalName
        //             };

        //             var createMultipleRequest = new CreateMultipleRequest
        //             {
        //                 Targets = employeeCollection
        //             };

        //             await Task.Run(() => client.Execute(createMultipleRequest));

        //             _logger.LogInformation("Created batch {Current}/{Total}", Math.Min(i + batchSize, entities.Count), entities.Count);
        //         }

        //         _logger.LogInformation("Successfully created all Employee records");
        //     }
        //     catch (Exception ex)
        //     {
        //         _logger.LogError(ex, "Failed to create employee records in Dataverse");
        //         throw;
        //     }
        // }

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