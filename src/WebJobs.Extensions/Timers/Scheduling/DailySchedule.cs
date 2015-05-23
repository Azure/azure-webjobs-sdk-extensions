using System;
using System.Collections.Generic;
using System.Linq;

namespace WebJobs.Extensions.Timers
{
    public class DailySchedule : TimerSchedule
    {
        private List<TimeSpan> schedule = new List<TimeSpan>();

        public DailySchedule()
        {
        }

        public DailySchedule(params string[] times)
        {
            schedule = times.Select(p => TimeSpan.Parse(p)).ToList();
        }

        public DailySchedule(params TimeSpan[] times)
        {
            schedule = times.ToList();
        }

        public void Add(TimeSpan time)
        {
            schedule.Add(time);
        }

        public override DateTime GetNextOccurrence(DateTime now)
        {
            if (schedule.Count == 0)
            {
                throw new InvalidOperationException("The schedule is empty.");
            }

            int idx = schedule.FindIndex(p => now.TimeOfDay <= p);
            if (idx == -1)
            {
                idx = 0;
            }

            TimeSpan nextTime = schedule[idx];
            return new DateTime(now.Year, now.Month, now.Day, nextTime.Hours, nextTime.Minutes, nextTime.Seconds);
        }
    }
}
