using Microsoft.Azure.WebJobs.Host.Converters;

namespace WebJobs.Extensions.Timers.Converters
{
    internal class TimerInfoOutputConverter<TInput> : IObjectToTypeConverter<TimerInfo> where TInput : class
    {
        private readonly IConverter<TInput, TimerInfo> _innerConverter;

        public TimerInfoOutputConverter(IConverter<TInput, TimerInfo> innerConverter)
        {
            _innerConverter = innerConverter;
        }

        public bool TryConvert(object input, out TimerInfo output)
        {
            TInput typedInput = input as TInput;

            if (typedInput == null)
            {
                output = null;
                return false;
            }

            output = _innerConverter.Convert(typedInput);
            return true;
        }
    }
}
