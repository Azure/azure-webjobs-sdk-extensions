// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    public enum DateParseHandling
    {
        // Summary:
        //     Date formatted strings are not parsed to a date type and are read as strings.
        None,

        // Summary:
        //     Date formatted strings will be parsed as per the default settings of Json.Net
        Default,
    }
}
