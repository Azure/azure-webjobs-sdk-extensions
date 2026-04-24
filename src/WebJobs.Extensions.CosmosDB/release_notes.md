## Microsoft.Azure.WebJobs.Extensions.CosmosDB 4.15.0

- Replace scaling warning/error log calls with standardized `LogFunctionScaleWarning` extension method to enable Scale Controller App Insights diagnostics
- Update Microsoft.Azure.WebJobs to 3.0.44

## Microsoft.Azure.WebJobs.Extensions.CosmosDB 4.14.1

- Fix CosmosClient memory leak in CosmosDbScalerProvider by implementing IDisposable (#988)

## Microsoft.Azure.WebJobs.Extensions.CosmosDB 4.14.0

- Update Microsoft.Azure.Cosmos to 3.56.0 (#979)
- Update Microsoft.Extensions.Azure to 1.13.1 (#979)

**NOTE**: The stable version of this package does not include CosmosDB preview features. Use the `-preview` versions of this package for CosmosDB preview service features.
