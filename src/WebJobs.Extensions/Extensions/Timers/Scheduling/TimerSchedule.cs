// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Extensions.Timers
{
    /// <summary>
    /// A timer scheduling strategy used with <see cref="TimerTriggerAttribute"/> for schedule
    /// based triggered jobs.
    /// </summary>
    public abstract class TimerSchedule
    {
        /// <summary>
        /// Gets the next occurrence of the schedule based on the specified
        /// base time.
        /// </summary>
        /// <param name="now">The time to compute the next schedule occurrence from.</param>
        /// <returns>The next schedule occurrence.</returns>
        public abstract DateTime GetNextOccurrence(DateTime now);

        /// <summary>
        /// Returns a collection of the next 'count' occurrences of the schedule,
        /// starting from now.
        /// </summary>
        /// <param name="count">The number of occurrences to return.</param>
        /// <returns>A collection of the next occurrences.</returns>
        /// <param name="now">The optional <see cref="DateTime"/> to start from.</param>
        public IEnumerable<DateTime> GetNextOccurrences(int count, DateTime? now = null)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException("count");
            }

            if (now == null)
            {
                now = DateTime.Now;
            }
            List<DateTime> occurrences = new List<DateTime>();
            for (int i = 0; i < count; i++)
            {
                DateTime next = GetNextOccurrence(now.Value);
                occurrences.Add(next);
                now = next + TimeSpan.FromMilliseconds(1);
            }

            return occurrences;
        }
    }
}
