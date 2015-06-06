using Microsoft.Azure.WebJobs.Host.Converters;

namespace Microsoft.Azure.WebJobs.Extensions.Timers.Converters
{
    internal class StringToTimerInfoConverter : IConverter<string, TimerInfo>
    {
        private readonly TimerSchedule _schedule;

        public StringToTimerInfoConverter(TimerSchedule schedule)
        {
            _schedule = schedule;
        }

        public TimerInfo Convert(string input)
        {
            return new TimerInfo(_schedule);
        }
    }
}
