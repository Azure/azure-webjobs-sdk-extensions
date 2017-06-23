// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Configuration;

namespace Microsoft.Azure.WebJobs.Extensions
{
    internal static class Utility
    {
        public const string AzureWebsiteSku = "WEBSITE_SKU";
        public const string DynamicSku = "Dynamic";

        /// <summary>
        /// Gets a value indicating whether the JobHost is running in a Dynamic
        /// App Service WebApp.
        /// </summary>
        public static bool IsDynamic
        {
            get
            {
                string value = Environment.GetEnvironmentVariable(AzureWebsiteSku);
                return string.Compare(value, DynamicSku, StringComparison.OrdinalIgnoreCase) == 0;
            }
        }
    }
}
