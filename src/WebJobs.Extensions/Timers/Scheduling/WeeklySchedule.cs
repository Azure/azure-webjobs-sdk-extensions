using System;
using System.Collections.Generic;
using System.Linq;

namespace WebJobs.Extensions.Timers
{
    public class WeeklySchedule : TimerSchedule
    {
        private List<TimeSpan>[] schedule = new List<TimeSpan>[7];

        protected void Add(DayOfWeek day, TimeSpan time)
        {
            List<TimeSpan> times = schedule[(int)day];
            if (times == null)
            {
                times = new List<TimeSpan>();
                schedule[(int)day] = times;
            }

            times.Add(time);
        }

        public override TimeSpan GetNextInterval(DateTime now)
        {
            // Determine where we are in the weekly schedule
            int idx = (int)now.DayOfWeek;
            List<TimeSpan> daySchedule = schedule[idx];
            TimeSpan nextTime = default(TimeSpan);
            if (daySchedule != null)
            {
                // we have a schedule for today
                // determine the next time
                nextTime = daySchedule.FirstOrDefault(p => p.TotalMilliseconds >= now.TimeOfDay.TotalMilliseconds);
            }

            // if we don't have a schedule for the current day,
            // or if we've already executed all occurrences for
            // today, advance to the next day with a schedule
            if (daySchedule == null || nextTime == default(TimeSpan))
            {
                while ((daySchedule = schedule[++idx % 7]) == null);

                // select the first time for the schedule
                nextTime = daySchedule[0];
            }

            // construct the next occurrence date
            int deltaDays = idx - (int)now.DayOfWeek;
            DateTime nextOccurrence = new DateTime(now.Year, now.Month, now.Day + deltaDays, nextTime.Hours, nextTime.Minutes, nextTime.Seconds);

            return nextOccurrence - now;
        }
    }
}
