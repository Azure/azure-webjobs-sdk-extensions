// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Extensions.Timers
{
    /// <summary>
    /// Represents a timer schedule status
    /// </summary>
    public class ScheduleStatus
    {
        /// <summary>
        /// The last recorded schedule occurrence
        /// </summary>
        public DateTime Last { get; set; }

        /// <summary>
        /// The expected next schedule occurrence
        /// </summary>
        public DateTime Next { get; set; }
    }
}
