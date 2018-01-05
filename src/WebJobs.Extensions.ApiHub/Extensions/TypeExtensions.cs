// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Extensions.ApiHub.Extensions
{
    internal static class TypeExtensions
    {
        public static string GetGenericTypeDisplayName(
            this Type genericType, 
            params string[] genericArgumentsNames)
        {
            return string.Format("{0}.{1}<{2}>",
                genericType.Namespace,
                genericType.Name.Split('`')[0],
                string.Join(",", genericArgumentsNames));
        }
    }
}
