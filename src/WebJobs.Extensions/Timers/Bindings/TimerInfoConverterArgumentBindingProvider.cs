using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Converters;

namespace Microsoft.Azure.WebJobs.Extensions.Timers.Bindings
{
    internal class TimerInfoConverterArgumentBindingProvider<T> : ITimerTriggerArgumentBindingProvider
    {
        private readonly IAsyncConverter<TimerInfo, T> _converter;

        public TimerInfoConverterArgumentBindingProvider(IAsyncConverter<TimerInfo, T> converter)
        {
            _converter = converter;
        }

        public IArgumentBinding<TimerInfo> TryCreate(ParameterInfo parameter)
        {
            if (parameter.ParameterType != typeof(T))
            {
                return null;
            }

            return new TimerConverterArgumentBinding(_converter);
        }

        internal class TimerConverterArgumentBinding : IArgumentBinding<TimerInfo>
        {
            private readonly IAsyncConverter<TimerInfo, T> _converter;

            public TimerConverterArgumentBinding(IAsyncConverter<TimerInfo, T> converter)
            {
                _converter = converter;
            }

            public Type ValueType
            {
                get { return typeof(T); }
            }

            public IReadOnlyDictionary<string, Type> BindingDataContract
            {
                get { return null; }
            }

            public async Task<IValueProvider> BindAsync(TimerInfo value, ValueBindingContext context)
            {
                object converted = await _converter.ConvertAsync(value, context.CancellationToken);
                IValueProvider provider = new TimerInfoValueProvider(converted, typeof(T));

                return provider;
            }
        }
    }
}
