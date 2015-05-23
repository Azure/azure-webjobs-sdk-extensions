using System;

namespace WebJobs.Extensions.Timers
{
    public class ConstantSchedule : TimerSchedule
    {
        private TimeSpan _due;
        private TimeSpan _interval;
        private TimeSpan? _intervalOverride;
        private bool _first = true;

        public ConstantSchedule(TimeSpan due, TimeSpan interval)
        {
            _due = due;
            _interval = interval;
        }

        public ConstantSchedule(TimeSpan period) 
            : this(TimeSpan.Zero, period)
        {
        }

        public TimeSpan Due
        {
            get { return _due; }
        }

        public TimeSpan Interval
        {
            get { return _interval; }
        }

        public override DateTime GetNextOccurrence(DateTime now)
        {
            TimeSpan nextInterval = _interval;
            if (_intervalOverride != null)
            {
                nextInterval = _intervalOverride.Value;
                _intervalOverride = null;
            }

            if (_first)
            {
                nextInterval += _due;
                _first = false;
            }

            return now + nextInterval;
        }

        public void SetNextInterval(TimeSpan interval)
        {
            _intervalOverride = interval;
        }
    }
}
