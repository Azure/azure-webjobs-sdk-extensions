using System;
using WebJobs.Extensions.Timers;

namespace WebJobsSandbox
{
    public class WeeklyPurgeSchedule : WeeklySchedule
    {
        public WeeklyPurgeSchedule()
        {
            // Every Monday at 8 AM
            Add(DayOfWeek.Monday, new TimeSpan(8, 0, 0));

            // Twice on Wednesdays at 9:30 AM and 9:00 PM
            Add(DayOfWeek.Wednesday, new TimeSpan(9, 30, 0));
            Add(DayOfWeek.Wednesday, new TimeSpan(21, 30, 0));

            // Every Friday at 10:00 AM
            Add(DayOfWeek.Friday, new TimeSpan(10, 0, 0));
        }
    }

    public class TimerSamples
    {
        public void TimerJob([TimerTrigger("00:00:10")] TimerInfo timer)
        {
            Console.WriteLine("Scheduled job fired!");
        }

        public void WeeklyTimerJob([TimerTrigger(typeof(WeeklyPurgeSchedule))] TimerInfo timer)
        {
            Console.WriteLine("Scheduled job fired!");
        }
    }
}
