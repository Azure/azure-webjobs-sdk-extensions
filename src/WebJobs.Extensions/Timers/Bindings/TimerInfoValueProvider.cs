using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;

namespace Microsoft.Azure.WebJobs.Extensions.Timers.Bindings
{
    internal class TimerInfoValueProvider : IValueProvider
    {
        private readonly object _value;
        private readonly Type _valueType;
        private readonly string _invokeString;

        private TimerInfoValueProvider(object value, Type valueType, string invokeString)
        {
            if (value != null && !valueType.IsAssignableFrom(value.GetType()))
            {
                throw new InvalidOperationException("value is not of the correct type.");
            }

            _value = value;
            _valueType = valueType;
            _invokeString = invokeString;
        }

        public Type Type
        {
            get { return _valueType; }
        }

        public object GetValue()
        {
            return _value;
        }

        public string ToInvokeString()
        {
            return _invokeString;
        }

        public static async Task<TimerInfoValueProvider> CreateAsync(TimerInfo clone, object value, Type valueType, CancellationToken cancellationToken)
        {
            string invokeString = await CreateInvokeStringAsync(clone, cancellationToken);
            return new TimerInfoValueProvider(value, valueType, invokeString);
        }

        private static Task<string> CreateInvokeStringAsync(TimerInfo clone, CancellationToken cancellationToken)
        {
            string invokeString = DateTime.UtcNow.ToString("o");

            return Task.FromResult<string>(invokeString);
        }
    }
}
