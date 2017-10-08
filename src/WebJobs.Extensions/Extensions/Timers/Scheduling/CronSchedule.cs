// Copyright (c) .NET Foundation. All rights reserved.
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
        /// Constructs a new instance based on the specified crontab schedule
        /// </summary>
        /// <param name="schedule">The crontab schedule to use</param>
        public CronSchedule(CrontabSchedule schedule)
        {
            _cronSchedule = schedule;
        }

        internal CrontabSchedule InnerSchedule
        {
            get
            {
                return _cronSchedule;
            }
        }

        /// <inheritdoc/>
        public override DateTime GetNextOccurrence(DateTime now)
        {
            return _cronSchedule.GetNextOccurrence(now);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "Cron: '{0}'", _cronSchedule.ToString());
        }

        internal static bool TryCreate(string cronExpression, out CronSchedule cronSchedule)
        {
            cronSchedule = null;
            
            CrontabSchedule.ParseOptions options = new CrontabSchedule.ParseOptions()
            {
                IncludingSeconds = HasSeconds(cronExpression)
            };

            CrontabSchedule crontabSchedule = CrontabSchedule.TryParse(cronExpression, options);
            if (crontabSchedule != null)
            {
                cronSchedule = new CronSchedule(crontabSchedule);
                return true;
            }
            return false;
        }

        private static bool HasSeconds(string cronExpression)
        {
            return cronExpression.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length != 5;
        }
    }
}
