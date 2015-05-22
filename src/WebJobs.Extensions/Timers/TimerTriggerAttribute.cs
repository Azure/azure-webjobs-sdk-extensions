using System;

namespace WebJobs.Extensions.Timers
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public class TimerTriggerAttribute : Attribute
    {
        /// <summary>
        /// Constructs a new instance using the <see cref="ConstantSchedule"/> <see cref="TimerSchedule"/>
        /// based on the specified inputs.
        /// </summary>
        /// <param name="period"></param>
        /// <param name="due"></param>
        public TimerTriggerAttribute(string period, string due = "00:00:00")
        {
            TimeSpan periodTimespan = TimeSpan.Parse(period);

            TimeSpan dueTimespan = new TimeSpan();
            if (due != null)
            {
                dueTimespan = TimeSpan.Parse(due);
            }

            Schedule = new ConstantSchedule(dueTimespan, periodTimespan);
        }

        /// <summary>
        /// Constructs a new instance using the specified <see cref="TimerSchedule"/> type.
        /// </summary>
        /// <param name="scheduleType"></param>
        public TimerTriggerAttribute(Type scheduleType)
        {
            Schedule = (TimerSchedule)Activator.CreateInstance(scheduleType);
        }

        public TimerSchedule Schedule { get; set; }
    }
}
