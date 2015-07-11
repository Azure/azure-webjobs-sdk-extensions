using System;

namespace Microsoft.Azure.WebJobs.Extensions.Extensions.Core
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
