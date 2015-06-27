namespace Microsoft.Azure.WebJobs.Extensions.Timers
{
    /// <summary>
    /// Provides access to timer information for jobs triggered by <see cref="TimerTriggerAttribute"/>
    /// </summary>
    public class TimerInfo
    {
        /// <summary>
        /// Constructs a new instances
        /// </summary>
        /// <param name="schedule">The timer trigger schedule.</param>
        public TimerInfo(TimerSchedule schedule)
        {
            Schedule = schedule;
        }

        /// <summary>
        /// Gets the schedule for the timer trigger.
        /// </summary>
        public TimerSchedule Schedule { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether this timer invocation
        /// is due to a missed schedule occurrence.
        /// </summary>
        public bool IsPastDue { get; set; }
    }
}
