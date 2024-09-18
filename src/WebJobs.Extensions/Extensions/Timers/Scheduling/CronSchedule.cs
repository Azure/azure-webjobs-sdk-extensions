﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Globalization;
using NCrontab;

namespace Microsoft.Azure.WebJobs.Extensions.Timers
{
    /// <summary>
    /// A scheduling strategy based on crontab expressions. <a href="http://en.wikipedia.org/wiki/Cron#CRON_expression"/>
    /// for details. E.g. "59 11 * * 1-5" is an expression representing every Monday-Friday at 11:59 AM.
    /// </summary>
    public class CronSchedule : TimerSchedule
    {
        private readonly CrontabSchedule _cronSchedule;

        /// <summary>
        /// Constructs a new instance based on the specified crontab schedule.
        /// </summary>
        /// <param name="schedule">The crontab schedule to use.</param>
        public CronSchedule(CrontabSchedule schedule)
        {
            _cronSchedule = schedule;
            var cron = schedule.ToString();
            var parts = cron.Split(' ');

            IsInterval = false;

            // in cron expressions, if any the first 3 parts (2 if not using seconds)
            // contain "*", "-", or "/" then it is considered an interval
            int partsToCheck = parts.Length == 6 ? 3 : 2;
            for (int i = 0; i < partsToCheck; i++)
            {
                var part = parts[i];
                if (part.Contains("*") ||
                    part.Contains("/") ||
                    part.Contains("-"))
                {
                    IsInterval = true;
                    break;
                }
            }
        }

        internal CrontabSchedule InnerSchedule
        {
            get
            {
                return _cronSchedule;
            }
        }

        internal bool IsInterval { get; private set; }

        /// <inheritdoc/>
        [Obsolete("This property is obsolete and will be removed in a future version.")]
        public override bool AdjustForDST => true;

        /// <inheritdoc/>        
        public override DateTime GetNextOccurrence(DateTime now)
        {
            // Note: TimeZoneInfo.Local is mocked in tests
            return GetNextOccurrence(new DateTimeOffset(now, TimeZoneInfo.Local.GetUtcOffset(now))).LocalDateTime;
        }

        private DateTimeOffset GetNextOccurrence(DateTimeOffset now)
        {
            var timeZone = TimeZoneInfo.Local;

            // The cron library only supports DateTime and does not take TimeZones into account. We need to 
            // do some manipulating of the time it returns to ensure we're taking the correct action during
            // transitions into and out of Daylight Savings Time.
            var nowDateTime = now.LocalDateTime;
            var nextDateTime = _cronSchedule.GetNextOccurrence(nowDateTime);

            // This will apply the correct offset for the local time zone based on the DateTime.
            var next = new DateTimeOffset(nextDateTime);

            // Now we need to evaluate the possibility of Daylight Savings transitions.
            // Example of DST transitions in 2018 for reference (from .NET source):
            //
            //         -=-=-=-=-=- Pacific Standard Time -=-=-=-=-=-=-
            //   March 11, 2018                            November 4, 2018
            // 2AM            3AM                        1AM              2AM
            // |      +1 hr     |                        |       -1 hr      |
            // | <invalid time> |                        | <ambiguous time> |
            //                  [========== DST ========>)
            if (TryAdjustInvalidTime(next, out DateTimeOffset? adjusted))
            {
                // "Spring Forward": "next" was invalid and was adjusted forward to a valid time.
                next = adjusted.Value;
            }
            else if (TryAdjustAmbiguousTime(now, next, out adjusted))
            {
                // "Fall Back": Either "now" or "next" was ambiguous and the time was adjusted.
                next = adjusted.Value;
            }

            return next;
        }

        /// <summary>
        /// If the provided DateTime is not valid, moves forward to the next occurrence in the schedule
        /// until a valid DateTime is found. For example, 02:30 does not exist when "springing forward"
        /// to Daylight Savings Time in the Pacific Standard Time Zone. Uses TimeZoneInfo.Local to make
        /// the determination.
        /// </summary>
        /// <param name="next">The DateTimeOffset to evaluate.</param>
        /// <param name="adjusted">The adjusted DateTimeOffset, if "next" is invalid.</param>
        /// <returns></returns>
        internal bool TryAdjustInvalidTime(DateTimeOffset next, out DateTimeOffset? adjusted)
        {
            var nextDateTime = next.LocalDateTime;
            bool isInvalidTime = TimeZoneInfo.Local.IsInvalidTime(nextDateTime);
            adjusted = null;

            if (!isInvalidTime)
            {
                return false;
            }

            while (isInvalidTime)
            {
                nextDateTime = _cronSchedule.GetNextOccurrence(nextDateTime);
                isInvalidTime = TimeZoneInfo.Local.IsInvalidTime(nextDateTime);
            }

            adjusted = new DateTimeOffset(nextDateTime);
            return true;
        }

        internal bool TryAdjustAmbiguousTime(DateTimeOffset now, DateTimeOffset next, out DateTimeOffset? adjusted)
        {
            adjusted = null;

            TimeZoneInfo timeZone = TimeZoneInfo.Local;
            if (!timeZone.SupportsDaylightSavingTime)
            {
                // There cannot be ambiguous times if there is no Daylight Saving Time.
                return false;
            }

            // Begin evaluating scenarios when Daylight Savings ends and time falls back. This leads to "ambiguous" times
            // as the clock repeats an hour. For example, times from 1:00 - 1:59 AM will occur twice in Pacific Standard Time
            // and are therefore considered "ambiguous" for this time zone.            
            bool isNowAmbiguous = timeZone.IsAmbiguousTime(now);
            bool isNextAmbiguous = timeZone.IsAmbiguousTime(next);

            if (!isNowAmbiguous && !isNextAmbiguous)
            {
                // There are no ambiguous times to adjust.
                return false;
            }

            // We also need to differentiate between "interval" and "point-in-time" schedules when exiting Daylight Savings Time.
            //
            // For "interval" schedules, we want to continue running through this ambiguous hour as usual. Using an "every 30 minute" schedule
            // and Pacific time offset an example, we'd want to:
            //    - Run at 01:00-7, 01:30-7, 01:00-8, 01:30-8, 02:00-8, 02:30-8, etc.
            //
            // For "point-in-time" schedules, we only want to run them once (i.e a 1:30 trigger only runs once on this day). Using an
            // "every day at 01:30" schedule and Pacific time offset as an example in 2018, we'd want to:
            //    - Run on November 3rd at 01:30-7
            //    - Run on November 4th at 01:30-7 (and not run at 1:30-8)
            //    - Run on November 5th at 01:30-8
            if (!IsInterval && isNextAmbiguous && next.Offset != now.Offset)
            {
                // "Fall Back" Scenario 1 -- Point-in-time schedule where the next time is ambiguous and the offsets have changed.
                //   This means that "now" is in DST and "next" is in Standard Time. Since we never want to run an ambiguous
                //   point-in-time schedule on Standard time, move the offset back to the Daylight offset.
                var offsetDiff = next.Offset - now.Offset;
                adjusted = TimeZoneInfo.ConvertTime(next.Add(offsetDiff), timeZone);
            }
            else if (!IsInterval && isNextAmbiguous && next.Offset == now.Offset)
            {
                // "Fall Back" Scenario 2 -- Point-in-time where "next" is ambiguous and "now" and "next" are in Standard time. This can happen
                //   when the timer starts and we are already past the "fall back" point. For example, if the trigger starts up at
                //   01:29-8 and we have a "every day at 01:30" schedule, we have to assume that we've already run it at 01:30-7 and
                //   therefore calculate the time after this.
                var nextDateTime = next.LocalDateTime;

                while (timeZone.IsAmbiguousTime(nextDateTime))
                {
                    nextDateTime = _cronSchedule.GetNextOccurrence(nextDateTime);
                }

                adjusted = new DateTimeOffset(nextDateTime);
            }
            else if (IsInterval && (isNowAmbiguous || isNextAmbiguous) && next.Offset != now.Offset)
            {
                // "Fall Back" Scenario 3 -- Interval schedule where either point is ambiguous and we're crossing the offset boundary. For example, 
                //   an "every 30 minute" interval in Pacific time:
                //   - 00:30-7 -> 01:00-8 (because all ambiguous "next" use Standard time) -> adjust back 01:00-7
                //   - 01:30-7 -> 02:00-8 -> adjust back to 01:00-8
                var offsetDiff = next.Offset - now.Offset;
                adjusted = TimeZoneInfo.ConvertTime(next.Add(offsetDiff), timeZone);
            }

            return adjusted != null;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "Cron: '{0}'", _cronSchedule.ToString());
        }

        internal static bool TryCreate(string cronExpression, out CronSchedule cronSchedule)
        {
            cronSchedule = null;

            if (cronExpression != null)
            {
                var options = CreateParseOptions(cronExpression);

                CrontabSchedule crontabSchedule = CrontabSchedule.TryParse(cronExpression, options);
                if (crontabSchedule != null)
                {
                    cronSchedule = new CronSchedule(crontabSchedule);
                    return true;
                }
            }
            return false;
        }

        private static CrontabSchedule.ParseOptions CreateParseOptions(string cronExpression)
        {
            var options = new CrontabSchedule.ParseOptions()
            {
                IncludingSeconds = HasSeconds(cronExpression)
            };

            return options;
        }

        private static bool HasSeconds(string cronExpression)
        {
            return cronExpression.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length != 5;
        }
    }
}
