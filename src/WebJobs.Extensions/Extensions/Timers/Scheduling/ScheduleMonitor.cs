using System;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.Timers.Scheduling
{
    /// <summary>
    /// This class is used to monitor and record schedule occurrences. It stores
    /// schedule occurrence info to persistent storage at runtime.
    /// <see cref="TimerTriggerAttribute"/> uses this class to monitor
    /// schedules to avoid missing scheduled executions.
    /// </summary>
    public abstract class ScheduleMonitor
    {
        /// <summary>
        /// Determines whether the schedule is currently past due.
        /// </summary>
        /// <param name="timerName">The name of the timer to check</param>
        /// <param name="now">The time to check</param>
        /// <param name="schedule">The <see cref="TimerSchedule"/></param>
        /// <returns>True if the schedule is past due, false otherwise.</returns>
        public abstract Task<bool> IsPastDueAsync(string timerName, DateTime now, TimerSchedule schedule);

        /// <summary>
        /// Updates the schedule status for the specified timer.
        /// </summary>
        /// <param name="timerName">The name of the timer</param>
        /// <param name="lastOccurrence">The last occurrence of the schedule (typically now)</param>
        /// <param name="nextOccurrence">The next occurrence of the schedule</param>
        public abstract Task UpdateAsync(string timerName, DateTime lastOccurrence, DateTime nextOccurrence);
    }
}
