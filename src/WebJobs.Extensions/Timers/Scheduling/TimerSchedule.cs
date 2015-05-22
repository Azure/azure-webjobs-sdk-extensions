using System;

namespace WebJobs.Extensions.Timers
{
    public abstract class TimerSchedule
    {
        public abstract TimeSpan GetNextInterval(DateTime now);
    }
}
