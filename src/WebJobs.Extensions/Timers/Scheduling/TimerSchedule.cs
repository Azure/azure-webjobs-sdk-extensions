using System;

namespace WebJobs.Extensions.Timers
{
    public abstract class TimerSchedule
    {
        public abstract DateTime GetNextOccurrence(DateTime now);
    }
}
