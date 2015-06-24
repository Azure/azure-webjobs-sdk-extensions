using System;
using Microsoft.Azure.WebJobs.Host.Bindings;

namespace Microsoft.Azure.WebJobs.Extensions.Timers.Bindings
{
    internal class TimerInfoValueProvider : IValueProvider
    {
        private readonly object _value;
        private readonly Type _valueType;

        public TimerInfoValueProvider(object value, Type valueType)
        {
            if (value != null && !valueType.IsAssignableFrom(value.GetType()))
            {
                throw new InvalidOperationException("value is not of the correct type.");
            }

            _value = value;
            _valueType = valueType;
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
            return DateTime.UtcNow.ToString("o");
        }
    }
}
