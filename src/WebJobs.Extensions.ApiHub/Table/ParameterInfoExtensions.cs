// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Reflection;

namespace Microsoft.Azure.WebJobs.Extensions.ApiHub.Table
{
    internal static class ParameterInfoExtensions
    {
        public static ApiHubTableAttribute GetTableAttribute(this ParameterInfo parameter)
        {
            return parameter.GetCustomAttribute<ApiHubTableAttribute>(inherit: false);
        }
    }
}
