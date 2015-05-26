using System;
using System.Collections.Generic;
using System.Linq;

namespace WebJobs.Extensions.Timers
{
    /// <summary>
    /// A simple weekly schedule. The schedule repeats each week, and each week,
    /// it cycles through the specified set of daily schedule settings.
    /// </summary>
    public class WeeklySchedule : TimerSchedule
    {
        private List<TimeSpan>[] schedule = new List<TimeSpan>[7];

        /// <summary>
        /// Adds the specified day/time occurrence to the schedule.
        /// </summary>
        /// <param name="day">The day to add the occurrence on.</param>
        /// <param name="time">The time of the occurrence.</param>
        protected internal void Add(DayOfWeek day, TimeSpan time)
        {
            List<TimeSpan> times = schedule[(int)day];
            if (times == null)
            {
                times = new List<TimeSpan>();
                schedule[(int)day] = times;
            }

            // sorted insertion
            int i;
            for (i = 0; i < times.Count && time > times[i]; i++)
            {
            }

            times.Insert(i, time);
        }

        /// <inheritdoc/>
        public override DateTime GetNextOccurrence(DateTime now)
        {
            if (schedule.All(p => p == null))
            {
                throw new InvalidOperationException("The schedule is empty.");
            }

            // Determine where we are in the weekly schedule
            int day = (int)now.DayOfWeek;
            List<TimeSpan> daySchedule = schedule[day];
            TimeSpan nextTime = default(TimeSpan);
            int nextTimeIndex = -1;
            if (daySchedule != null)
            {
                // we have a schedule for today
                // determine the next time
                nextTimeIndex = daySchedule.FindIndex(p => p.TotalMilliseconds >= now.TimeOfDay.TotalMilliseconds);
                if (nextTimeIndex > 0)
                {
                    nextTime = daySchedule[nextTimeIndex];
                }
            }

            // if we don't have a schedule for the current day,
            // or if we've already executed all occurrences for
            // today, advance to the next day with a schedule
            if (daySchedule == null || nextTimeIndex == -1)
            {
                while ((daySchedule = schedule[++day % 7]) == null)
                {
                }

                // select the first time for the schedule
                nextTime = daySchedule[0];
            }

            // construct the next occurrence date
            int deltaDays = day - (int)now.DayOfWeek;
            DateTime nextOccurrence = new DateTime(now.Year, now.Month, now.Day, nextTime.Hours, nextTime.Minutes, nextTime.Seconds);
            nextOccurrence = nextOccurrence.AddDays(deltaDays);

            return nextOccurrence;
        }
    }
}
