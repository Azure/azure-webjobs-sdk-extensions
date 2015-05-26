using System;
using WebJobs.Extensions.Timers;

namespace WebJobs.Extensions.Timers
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class TimerTriggerAttribute : Attribute
    {
        /// <summary>
        /// Constructs a new instance based on the schedule expression passed in./>
        /// </summary>
        /// <param name="expression">A schedule expression. This can either be a crontab expression 
        /// <a href="http://en.wikipedia.org/wiki/Cron#CRON_expression"/> or a <see cref="TimeSpan"/> string.</param>
        public TimerTriggerAttribute(string expression)
        {
            CronSchedule cronSchedule = null;
            if (CronSchedule.TryCreate(expression, out cronSchedule))
            {
                Schedule = cronSchedule;
            }
            else
            {
                TimeSpan periodTimespan = TimeSpan.Parse(expression);
                Schedule = new ConstantSchedule(periodTimespan);
            }
        }

        /// <summary>
        /// Constructs a new instance using the specified <see cref="TimerSchedule"/> type.
        /// </summary>
        /// <param name="scheduleType">The type of schedule to create.</param>
        public TimerTriggerAttribute(Type scheduleType)
        {
            Schedule = (TimerSchedule)Activator.CreateInstance(scheduleType);
        }

        /// <summary>
        /// Gets the Schedule.
        /// </summary>
        public TimerSchedule Schedule { get; private set; }
    }
}
