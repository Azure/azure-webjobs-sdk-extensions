// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.EasyTables
{
    internal static class EasyTableUtility
    {
        /// <summary>
        /// Gets the core type of the specified type. Then validates that it is
        /// usable with Easy Tables.
        /// </summary>
        /// <param name="type">The type to evaluate.</param>
        /// <returns></returns>
        public static bool IsCoreTypeValidItemType(Type type)
        {
            Type coreType = TypeUtility.GetCoreType(type);
            return IsValidItemType(coreType);
        }

        /// <summary>
        /// Evaluates whether the specified type is valid for use with EasyTables. The type
        /// must contain a single public string 'Id' property or be of type JObject.
        /// </summary>
        /// <param name="itemType">The type to evaluate.</param>
        /// <returns></returns>
        public static bool IsValidItemType(Type itemType)
        {
            if (itemType == typeof(JObject))
            {
                return true;
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