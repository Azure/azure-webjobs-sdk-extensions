// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Globalization;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Trigger
{
    internal static class MaxItemsPerInvocationResolver
    {
        internal static int Resolve(int explicitValue, string settingName, string defaultValue, Func<string, string> resolveSetting, string functionName)
        {
            int? appDefault = defaultValue == null ? (int?)null : ParsePositive(defaultValue, "DefaultMaxItemsPerInvocation");
            if (settingName != null && explicitValue > 0)
            {
                throw new InvalidOperationException($"Function '{functionName}' cannot specify both MaxItemsPerInvocation and MaxItemsPerInvocationSetting.");
            }

            if (settingName != null)
            {
                if (string.IsNullOrWhiteSpace(settingName))
                {
                    throw new InvalidOperationException($"Function '{functionName}' has an empty MaxItemsPerInvocationSetting.");
                }

                return ParsePositive(resolveSetting?.Invoke(settingName), $"MaxItemsPerInvocationSetting '{settingName}' for function '{functionName}'");
            }

            return explicitValue > 0 ? explicitValue : appDefault ?? explicitValue;
        }

        private static int ParsePositive(string value, string option)
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) || result <= 0)
            {
                throw new InvalidOperationException($"{option} must resolve to a positive 32-bit integer.");
            }

            return result;
        }
    }
}
