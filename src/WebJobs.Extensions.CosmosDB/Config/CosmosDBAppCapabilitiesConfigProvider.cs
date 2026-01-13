// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Script.AppCapabilities;
using Microsoft.Extensions.Options;

internal sealed class CosmosDBAppCapabilitiesConfigProvider : IConfigureOptions<AppCapabilitiesOptions>
{
    public void Configure(AppCapabilitiesOptions options)
    {
        // Add or update a capability for your extension
        var metadata = new Dictionary<string, string>
        {
            ["endpoint"] = "https://your-extension-endpoint",
            ["featureFlag"] = "true"
        };

        AppCapabilityHelpers.AddOrUpdateCapability(
            options.Capabilities,
            "CosmosDBCapability",
            CapabilitySourceNames.ExtensionSource,
            version: "1.0",
            metadata: metadata);
    }
}