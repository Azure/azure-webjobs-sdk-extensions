﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.WebJobs.Host;

namespace Microsoft.Azure.WebJobs.Extensions.Timers
{
    /// <summary>
    /// A timer scheduling strategy used with <see cref="TimerTriggerAttribute"/> for schedule
    /// based triggered jobs.
    /// </summary>
    public abstract class TimerSchedule
    {
        /// <summary>
        /// Gets a value indicating whether intervals between invocations should account for DST.        
        /// </summary>
        public abstract bool AdjustForDST { get; }

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
                now = next;
            }

            return occurrences;
        }

        internal static TimerSchedule Create(TimerTriggerAttribute attribute, INameResolver nameResolver)
        {
            TimerSchedule schedule = null;

            if (!string.IsNullOrEmpty(attribute.ScheduleExpression))
            {
                string resolvedExpression = nameResolver.ResolveWholeString(attribute.ScheduleExpression);
                if (CronSchedule.TryCreate(resolvedExpression, out CronSchedule cronSchedule))
                {
                    schedule = cronSchedule;

                    DateTime[] nextOccurrences = cronSchedule.InnerSchedule.GetNextOccurrences(DateTime.Now, DateTime.Now + TimeSpan.FromMinutes(1)).ToArray();
                    if (nextOccurrences.Length > 1)
                    {
                        // if there is more than one occurrence due in the next minute,
                        // assume that this is a sub-minute constant schedule and disable
                        // persistence
                        attribute.UseMonitor = false;
                    }
                }
                else if (TimeSpan.TryParse(resolvedExpression, out TimeSpan periodTimespan))
                {
                    schedule = new ConstantSchedule(periodTimespan);

                    if (periodTimespan.TotalMinutes < 1)
                    {
                        // for very frequent constant schedules, we want to disable persistence
                        attribute.UseMonitor = false;
                    }
                }
                else
                {
                    throw new ArgumentException(string.Format("The schedule expression '{0}' was not recognized as a valid cron expression or timespan string.", resolvedExpression));
                }
            }
            else
            {
                schedule = (TimerSchedule)Activator.CreateInstance(attribute.ScheduleType);
            }

            return schedule;
        }
    }
}
