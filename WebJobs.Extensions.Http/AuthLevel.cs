// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace WebJobs.Extensions.Http
{
    /// <summary>
    /// Enum for available authentication levels for an http triggered function.
    /// </summary>
    public enum AuthLevel
    {
        Anonymous,
        Function,
        Admin
    }
}
