using System;
using WebJobs.Extensions.Timers;
using Xunit;

namespace WebJobs.Extensions.Tests.Timers
{
    public class TimerTriggerAttributeTests
    {
        [Fact]
        public void Constructor_CreatesConstantSchedule()
        {
            TimerTriggerAttribute attribute = new TimerTriggerAttribute("00:00:15");

            Assert.Equal(typeof(ConstantSchedule), attribute.Schedule.GetType());
            
            DateTime now = DateTime.Now;
            DateTime nextOccurrence = attribute.Schedule.GetNextOccurrence(now);
            Assert.Equal(new TimeSpan(0, 0, 15), nextOccurrence - now);
        }

        [Fact]
        public void Constructor_CreatesCronSchedule()
        {
            TimerTriggerAttribute attribute = new TimerTriggerAttribute("*/15 * * * * *");

            Assert.Equal(typeof(CronSchedule), attribute.Schedule.GetType());

            DateTime now = new DateTime(2015, 5, 22, 9, 45, 00);
            DateTime nextOccurrence = attribute.Schedule.GetNextOccurrence(now);
            Assert.Equal(new TimeSpan(0, 0, 15), nextOccurrence - now);
        }
    }
}
