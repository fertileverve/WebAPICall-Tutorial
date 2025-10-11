using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;


namespace APITest.Dataverse
{
    public class DataverseProcessor
    {
        // Connection for Dataverse
        public ServiceClient? _serviceClient;

        public void DataverseInitialize(string connectionString)
        {
            _serviceClient = new ServiceClient(connectionString);

            if (!(_serviceClient != null && _serviceClient.IsReady))
            {
                throw new InvalidOperationException("Cannot access Dataverse.");
            }
        }

        public void CreateEntities(List<Entity> entities)
        {
            try
            {
                if (_serviceClient != null && _serviceClient.IsReady)
                {
                    EntityCollection employeeCollection = new EntityCollection(entities)
                    {
                        EntityName = entities[0].LogicalName
                    };
                    CreateMultipleRequest createMultipleRequest = new CreateMultipleRequest
                    {
                        Targets = employeeCollection
                    };

                    CreateMultipleResponse createMultipleResponse = (CreateMultipleResponse)_serviceClient.Execute(createMultipleRequest);
                }
                else
                {
                    throw new InvalidOperationException("Cannot access Dataverse.");
                }
            }
            catch (Exception ex)
            {
                Program.DefaultException(ex);
            }
        }

        public void PerformServiceOperation()
        {
            if (_serviceClient != null && _serviceClient.IsReady)
            {
                // Use _serviceClient to interact with the service
                // For example: _serviceClient.Create(entity);
                Console.WriteLine("ServiceClient is ready and performing operation.");
            }
            else
            {
                Console.WriteLine("ServiceClient is not initialized or not ready.");
            }
        }

        public void ShowEntities(List<Entity> entities)
        {
            foreach (Entity entity in entities)
            {
                Console.WriteLine($"Entity ID: {entity["crfbe_name"]}");
            }
        }

        public void DataverseTest()
        {
            try
            {
                if (_serviceClient != null && _serviceClient.IsReady)
                {
                    QueryExpression query = new QueryExpression("account");
                    query.ColumnSet = new ColumnSet(true);

                    EntityCollection accounts = _serviceClient.RetrieveMultiple(query);

                    foreach (Entity account in accounts.Entities)
                    {
                        Console.WriteLine($"Name: {account.GetAttributeValue<string>("name")}");
                    }
                }
                else
                {
                    throw new InvalidOperationException("Cannot access Dataverse.");
                }
            }
            catch (Exception ex)
            {
                Program.DefaultException(ex);
            }
        }

        public Dictionary<string, int> GetChoiceMap(string entityName, string attributeName)
        {
            Dictionary<string, int> choiceMap = new();

            var retrieveAttributeRequest = new RetrieveAttributeRequest
            {
                EntityLogicalName = entityName,
                LogicalName = attributeName,
                MetadataId = System.Guid.Empty,
                RetrieveAsIfPublished = true
            };

            if (_serviceClient != null && _serviceClient.IsReady)
            {
                var retrieveAttributeResponse = (RetrieveAttributeResponse)_serviceClient.Execute(retrieveAttributeRequest);

                if (retrieveAttributeResponse.AttributeMetadata is EnumAttributeMetadata choiceMetadata)
                {
                    // 4. Extract the choices
                    foreach (var option in choiceMetadata.OptionSet.Options)
                    {
                        string label = option.Label.UserLocalizedLabel.Label.ToLower();
                        int value = (option.Value != null) ? option.Value.Value : 0;

                        choiceMap.Add(label, value);
                    }
                }
            }
            else
            {
                throw new InvalidOperationException("Cannot access Dataverse.");
            }

            return choiceMap;
        }
    }
}