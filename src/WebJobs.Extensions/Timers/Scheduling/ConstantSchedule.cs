using System;

namespace Microsoft.Azure.WebJobs.Extensions.Timers
{
    /// <summary>
    /// A simple constant interval scheduling strategy.
    /// </summary>
    public class ConstantSchedule : TimerSchedule
    {
        private TimeSpan _interval;
        private TimeSpan? _intervalOverride;

        /// <summary>
        /// Constructs an instance using the specified interval.
        /// </summary>
        /// <param name="interval">The constant interval between schedule occurrences.</param>
        public ConstantSchedule(TimeSpan interval)
        {
            _interval = interval;
        }

        /// <inheritdoc/>
        public override DateTime GetNextOccurrence(DateTime now)
        {
            TimeSpan nextInterval = _interval;
            if (_intervalOverride != null)
            {
                nextInterval = _intervalOverride.Value;
                _intervalOverride = null;
            }

            return now + nextInterval;
        }

        /// <summary>
        /// Override the next schedule interval using the specified interval.
        /// </summary>
        /// <param name="interval">The one time interval to use for the next occurrence.</param>
        public void SetNextInterval(TimeSpan interval)
        {
            _intervalOverride = interval;
        }
    }
}
