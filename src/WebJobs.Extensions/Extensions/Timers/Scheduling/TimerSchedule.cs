// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

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
        [Obsolete("This property is obsolete and will be removed in a future version. All TimerSchedule implementations should now handle their own DST transitions.")]
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

        internal static TimerSchedule Create(TimerTriggerAttribute attribute, INameResolver nameResolver, ILogger logger)
        {
            TimerSchedule schedule = null;

            if (!string.IsNullOrEmpty(attribute.ScheduleExpression))
            {
                string resolvedExpression = nameResolver.ResolveWholeString(attribute.ScheduleExpression);
                if (CronSchedule.TryCreate(resolvedExpression, logger, out CronSchedule cronSchedule))
                {
                    schedule = cronSchedule;
                    if (attribute.UseMonitor && ShouldDisableScheduleMonitor(cronSchedule, DateTime.Now))
                    {
                        logger.LogDebug("UseMonitor changed to false based on schedule frequency.");
                        attribute.UseMonitor = false;
                    }
                }
                else if (TimeSpan.TryParse(resolvedExpression, out TimeSpan periodTimespan))
                {
                    schedule = new ConstantSchedule(periodTimespan);

                    if (attribute.UseMonitor && periodTimespan.TotalMinutes < 1)
                    {
                        // for very frequent constant schedules, we want to disable persistence
                        logger.LogDebug("UseMonitor changed to false based on schedule frequency.");
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

        /// <summary>
        /// For schedules with an occurrence more frequent than 1 minute, we disable schedule monitoring.
        /// </summary>
        /// <param name="cronSchedule">The cron schedule to check.</param>
        /// <returns>True if monitoring should be disabled, false otherwise.</returns>
        internal static bool ShouldDisableScheduleMonitor(CronSchedule cronSchedule, DateTime now)
        {
            // take the original expression minus the seconds portion
            string expression = cronSchedule.InnerSchedule.ToString();
            var expressions = expression.Split(' ');

            // If any of the minute or higher fields contain non-wildcard expressions
            // the schedule can be longer than 1 minute. I.e. the only way for all occurrences
            // to be less than or equal to a minute is if all these fields are wild ("* * * * *").
            bool hasNonSecondRestrictions = !expressions.Skip(1).All(p => p == "*");

            if (!hasNonSecondRestrictions)
            {
                // If to here, we know we're dealing with a schedule of the form X * * * * *
                // so we just need to consider the seconds expression to determine if it occurs
                // more frequently than 1 minute.
                // E.g. an expression like */10 * * * * * occurs every 10 seconds, while an
                // expression like 0 * * * * * occurs exactly once per minute.
                DateTime[] nextOccurrences = cronSchedule.InnerSchedule.GetNextOccurrences(now, now + TimeSpan.FromMinutes(1)).ToArray();
                if (nextOccurrences.Length > 1)
                {
                    // if there is more than one occurrence due in the next minute,
                    // assume that this is a sub-minute constant schedule and disable
                    // persistence
                    return true;
                }
            }

            return false;
        }
    }
}
