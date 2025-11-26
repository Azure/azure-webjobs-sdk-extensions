## Microsoft.Azure.WebJobs.Extensions.CosmosDB 4.13.0-preview.1

- Update Microsoft.Azure.Cosmos to 3.55.0-preview.1. (#976)
- Fix type binding for `AllVersionsAndDelete` mode. Throw exception if type is not `ChangeFeedMode<T>`. (#976)

**NOTE**: The preview version of this package is released in parallel with the stable train. This follows Microsoft.Azure.Cosmos release model, where preview packages contain extra CosmosDB preview service features.
