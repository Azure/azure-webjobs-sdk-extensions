using System;

namespace WebJobs.Extensions.Timers
{
    public class ConstantSchedule : TimerSchedule
    {
        private TimeSpan _due;
        private TimeSpan _period;
        private TimeSpan? _intervalOverride;

        public ConstantSchedule(TimeSpan due, TimeSpan period)
        {
            _due = due;
            _period = period;
        }

        public override TimeSpan GetNextInterval(DateTime now)
        {
            if (_intervalOverride != null)
            {
                TimeSpan nextInterval = _intervalOverride.Value;
                _intervalOverride = null;
                return nextInterval;
            }
            return _due + _period;
        }

        public void SetNextInterval(TimeSpan interval)
        {
            _intervalOverride = interval;
        }
    }
}
