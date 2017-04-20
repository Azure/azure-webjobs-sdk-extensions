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

        /// <summary>
        /// Gets or sets the name of the function being invoked
        /// </summary>
        public string FunctionName { get; set; }

        /// <summary>
        /// Gets or sets the function directory
        /// </summary>
        public string FunctionDirectory { get; set; }
    }
}
