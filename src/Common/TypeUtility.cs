// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs
{
    internal static class TypeUtility
    {
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

                throw new InvalidOperationException("Binding parameter types can only have one generic argument.");
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