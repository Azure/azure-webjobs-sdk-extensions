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
                string value = GetSettingFromConfigOrEnvironment(AzureWebsiteSku);
                return string.Compare(value, DynamicSku, StringComparison.OrdinalIgnoreCase) == 0;
            }
        }

        public static string GetSettingFromConfigOrEnvironment(string settingName)
        {
            string configValue = ConfigurationManager.AppSettings[settingName];
            if (!string.IsNullOrEmpty(configValue))
            {
                // config values take precedence over environment values
                return configValue;
            }

            return Environment.GetEnvironmentVariable(settingName) ?? configValue;
        }
    }
}
