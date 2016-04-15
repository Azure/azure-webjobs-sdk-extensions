// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs;

namespace System.Reflection
{
    internal static class ParameterInfoExtensions
    {
        public static MobileTableAttribute GetMobileTableAttribute(this ParameterInfo parameter)
        {
            return parameter.GetCustomAttribute<MobileTableAttribute>(inherit: false);
        }
    }
}
