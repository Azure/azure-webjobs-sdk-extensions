using System;
using System.Collections.Generic;
using System.Linq;

namespace WebJobs.Extensions.Timers
{
    public class MonthlySchedule : TimerSchedule
    {
        private List<TimeSpan>[] schedule = new List<TimeSpan>[30];

        public void Add(int dayOfMonth, TimeSpan time)
        {
            if (dayOfMonth < -1 || dayOfMonth > 28)
            {
                throw new ArgumentOutOfRangeException("dayOfMonth");
            }

            if (dayOfMonth == -1)
            {
                // slot for "last day of month"
                dayOfMonth = 29;
            }

            List<TimeSpan> times = schedule[dayOfMonth];
            if (times == null)
            {
                times = new List<TimeSpan>();
                schedule[dayOfMonth] = times;
            }

            times.Add(time);
        }

        public override DateTime GetNextOccurrence(DateTime now)
        {
            if (schedule.All(p => p == null))
            {
                throw new InvalidOperationException("The schedule is empty.");
            }

            // Determine where we are in the monthly schedule
            int idx = now.Day;
            if (idx > 28)
            {
                // shift back to our "last day"
                idx = 29;
            }
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
                while ((daySchedule = schedule[++idx % 30]) == null);

                // select the first time for the schedule
                nextTime = daySchedule[0];
            }

            // construct the next occurrence date
            int day = now.Day;
            int deltaDays = 0;
            if (idx == 29)
            {
                day = DateTime.DaysInMonth(now.Year, now.Month);
            }
            else if (idx > 29)
            {
                deltaDays = idx - now.Day + 1;
            }
            else
            {
                deltaDays = idx - now.Day;
            }
            DateTime nextOccurrence = new DateTime(now.Year, now.Month, day, nextTime.Hours, nextTime.Minutes, nextTime.Seconds);
            nextOccurrence = nextOccurrence.AddDays(deltaDays);

            if (nextOccurrence.Month > now.Month && nextOccurrence.Day != 1)
            {
                // we advanced to the next month, so ensure we're on the first day
                nextOccurrence = new DateTime(now.Year, now.Month, 1, nextTime.Hours, nextTime.Minutes, nextTime.Seconds);
            }

            return nextOccurrence;
        }
    }
}
