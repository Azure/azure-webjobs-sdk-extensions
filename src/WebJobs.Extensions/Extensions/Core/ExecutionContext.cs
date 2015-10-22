// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Provides context information for job function invocations.
    /// </summary>
    public class ExecutionContext
    {
        /// <summary>
        /// The job function invocation ID
        /// </summary>
        public Guid InvocationId { get; set; }
    }
}
