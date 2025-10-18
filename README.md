# Dataverse Employee Sync Console Application

This is a C# .NET console application built to demonstrate connecting to a public web API, retrieving data, and performing an intelligent synchronization with a Microsoft Dataverse instance. The primary use case is fetching a list of employee records from the [DummyJSON API](https://dummyjson.com/) and then creating or updating corresponding records in a custom `crfbe_employee` table in Dataverse.

This project started as a learning exercise and evolved to include modern .NET best practices, including dependency injection, secure configuration, advanced change tracking, and structured logging.

## Key Features 🚀

* **Modern .NET (net9.0)**: Built using the latest .NET framework with a minimal `Program.cs` host builder setup.
* **Dependency Injection**: Correctly uses DI to manage services for API access (`IApiService`), Dataverse (`IDataverseService`), and security (`IHostValidator`).
* **Intelligent Data Sync**: Performs a sophisticated sync operation, not just a blind upsert. It fetches existing Dataverse records and does a field-by-field comparison to only update records that have changed.
* **Detailed Audit Logging**: In addition to standard application logs, a separate audit log is generated (`logs/audit/`) that tracks the creation of new employees and details every specific field change for updated employees (e.g., `crfbe_email: [old@test.com] → [new@test.com]`).
* **Secure by Default**:
    * **Host Validation**: Includes a custom `IHostValidator` service to ensure all outbound HTTP requests are made *only* to domains present in the `AllowedHosts` configuration, preventing data leaks to unauthorized endpoints.
    * **HTTPS Enforced**: The validator also rejects any non-HTTPS URLs.
    * **Credential Management**: Configured to use .NET User Secrets for storing sensitive `ClientId` and `ClientSecret` values during development, keeping them out of source control.
* **Structured Logging**: Uses **Serilog** for robust, structured logging configured directly from `appsettings.json`, with outputs to both the console and rolling log files.
* **Clean Architecture**: Code is logically separated into distinct layers:
    * **Application (`App`)**: The main orchestration layer.
    * **API (`APIDummyJSON`)**: Data models and service for the external API.
    * **Dataverse (`Dataverse`)**: All logic for connecting and syncing data with Dataverse.
    * **Validator (`Validator`)**: Security-focused validation services.

---

## Project Architecture 🏗️

The application is bootstrapped in `Program.cs`, which sets up the .NET Generic Host, configures all services for dependency injection, and loads configuration from `appsettings.json`.

1.  The host requests the main `Application` class from the service provider.
2.  The `Application` class orchestrates the entire process.
3.  It first calls the `IApiService` (`DummyJSONService`) to fetch the list of `Employee` objects from the `dummyjson.com` API.
4.  It then calls the `IDataverseService` (`DataverseService`) to get a mapping of `crfbe_gender` choice labels to their integer values.
5.  The `Employee` list is converted into a `List<Entity>` for Dataverse.
6.  Finally, `IDataverseService.SyncEmployeesWithChangeTrackingAsync` is called, which contains the core logic to compare and sync the data.

---

## Core Logic: Change Tracking Sync 🔍

The application's most complex logic is in the `SyncEmployeesWithChangeTrackingAsync` method within `DataverseService.cs`. It does not use a simple `Upsert` for every record, as this would be inefficient and hide changes.

Instead, it performs the following steps:

1.  **Fetch Existing Data**: It first queries Dataverse to retrieve *all* existing `crfbe_employee` records into a `Dictionary<string, Entity>`, keyed by their `crfbe_id` (the ID from the API).
2.  **Compare and Sync**: It then iterates through the new list of employees from the API.
    * **If a new employee ID is NOT in the dictionary**: The record is marked as **new**. A new `EmployeeChangeRecord` is created, logged to the audit file, and the entity is created in Dataverse.
    * **If a new employee ID *is* in the dictionary**: The `CompareEntities` method is called to perform a field-by-field check between the new API data and the existing Dataverse entity.
    * **If changes are detected**: A change record detailing every field that changed (with old and new values) is logged. The entity is then updated in Dataverse.
    * **If no changes are detected**: The record is skipped, saving an unnecessary API call.
3.  **Log Summary**: A final summary of all
