// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.MobileApps
{
    internal static class MobileAppUtility
    {
        /// <summary>
        /// Gets the core type of the specified type. Then validates that it is
        /// usable with mobile tables.
        /// </summary>
        /// <param name="type">The type to evaluate.</param>
        /// <param name="context">The <see cref="MobileTableContext"/>.</param>
        /// <returns></returns>
        public static bool IsCoreTypeValidItemType(Type type, MobileTableContext context)
        {
            Type coreType = TypeUtility.GetCoreType(type);
            return IsValidItemType(coreType, context);
        }

        /// <summary>
        /// Evaluates whether the specified type is valid for use with mobile tables.
        /// If the type is <see cref="JObject"/>, then the ResolvedTableName property on 
        /// <see cref="MobileTableAttribute"/> is required.
        /// If the type is not <see cref="JObject"/>, then it must contain a single public
        /// string 'Id' property.
        /// </summary>
        /// <param name="itemType">The type to evaluate.</param>
        /// <param name="context">The <see cref="MobileTableContext"/>.</param>
        /// <returns></returns>
        public static bool IsValidItemType(Type itemType, MobileTableContext context)
        {
            // We cannot support a type of JObject without a TableName.
            if (itemType == typeof(JObject))
            {
                return !string.IsNullOrEmpty(context.ResolvedTableName);
            }

            // POCO types must have a string id property (case insensitive).
            IEnumerable<PropertyInfo> idProperties = itemType.GetProperties()
                .Where(p => string.Equals("id", p.Name, StringComparison.OrdinalIgnoreCase) && p.PropertyType == typeof(string));

            if (idProperties.Count() != 1)
            {
                return false;
            }

            return true;
        }
    }
}