using System;
using NCrontab;

namespace WebJobs.Extensions.Timers
{
    /// <summary>
    /// A scheduling strategy based on crontab expressions. <a href="http://en.wikipedia.org/wiki/Cron#CRON_expression"/>
    /// for details. E.g. "59 11 * * 1-5" is an expression representing every Monday-Friday at 11:59 AM.
    /// </summary>
    public class CronSchedule : TimerSchedule
    {
        private CrontabSchedule cronSchedule;

        public CronSchedule(string cronTabExpression)
            : this(CrontabSchedule.Parse(cronTabExpression))
        {
        }

        public CronSchedule(CrontabSchedule schedule)
        {
            cronSchedule = schedule;
        }

        public override DateTime GetNextOccurrence(DateTime now)
        {
            return cronSchedule.GetNextOccurrence(now);
        }

        public static bool TryCreate(string cronExpression, out CronSchedule cronSchedule)
        {
            cronSchedule = null;
            CrontabSchedule.ParseOptions options = new CrontabSchedule.ParseOptions()
            {
                IncludingSeconds = true
            };
            CrontabSchedule crontabSchedule = CrontabSchedule.TryParse(cronExpression, options);
            if (crontabSchedule != null)
            {
                cronSchedule = new CronSchedule(crontabSchedule);
                return true;
            }
            return false;
        }
    }
}
