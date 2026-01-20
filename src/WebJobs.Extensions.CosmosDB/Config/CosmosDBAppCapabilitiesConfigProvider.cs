// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Script.AppCapabilities;
using Microsoft.Extensions.Options;

internal sealed class CosmosDBAppCapabilitiesConfigProvider : IConfigureOptions<AppCapabilitiesOptions>
{
    public void Configure(AppCapabilitiesOptions options)
    {
        options.Capabilities["CosmosDBCapability"] = "value1";
        options.Capabilities["CosmosDBTriggerCapability"] = "value2";
    }
}