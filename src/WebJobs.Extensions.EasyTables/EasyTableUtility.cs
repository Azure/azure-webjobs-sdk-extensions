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
            Type coreType = GetCoreType(type);
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

        /// <summary>
        /// Returns the core EasyTable type for the supplied parameter.
        /// </summary>
        /// <remarks>
        /// For example, the core Type is T in the following parameters:
        /// <list type="bullet">
        /// <item><description><see cref="ICollector{T}"/></description></item>
        /// <item><description>T[]</description></item>
        /// <item><description>out T</description></item>
        /// <item><description>out T[]</description></item>
        /// </list>
        /// </remarks>
        /// <param name="type">The Type to evaluate.</param>
        /// <returns>The core Type</returns>
        public static Type GetCoreType(Type type)
        {
            Type coreType = type;
            if (coreType.IsByRef)
            {
                coreType = coreType.GetElementType();
            }

            if (coreType.IsArray)
            {
                return coreType.GetElementType();
            }

            if (coreType.IsGenericType)
            {
                Type genericArgType = null;
                if (TryGetSingleGenericArgument(coreType, out genericArgType))
                {
                    return genericArgType;
                }

                throw new InvalidOperationException("Easy Table parameter types can only have one generic argument.");
            }

            return coreType;
        }

        /// <summary>
        /// Checks whether the specified type has a single generic argument. If so,
        /// that argument is returned via the out parameter.
        /// </summary>
        /// <param name="genericType">The generic type.</param>
        /// <param name="genericArgumentType">The single generic argument.</param>
        /// <returns>true if there was a single generic argument. Otherwise, false.</returns>
        public static bool TryGetSingleGenericArgument(Type genericType, out Type genericArgumentType)
        {
            genericArgumentType = null;
            Type[] genericArgTypes = genericType.GetGenericArguments();

            if (genericArgTypes.Length != 1)
            {
                return false;
            }

            genericArgumentType = genericArgTypes[0];
            return true;
        }
    }
}