// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Linq;

namespace Microsoft.Azure.WebJobs.Extensions
{
    internal static class Utility
    {
        internal static string FirstOrDefault(params string[] values)
        {
            return values.FirstOrDefault(v => !string.IsNullOrEmpty(v));
        }
    }
}
