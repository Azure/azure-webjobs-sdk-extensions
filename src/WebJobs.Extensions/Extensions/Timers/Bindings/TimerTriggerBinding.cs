using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Timers.Config;
using Microsoft.Azure.WebJobs.Extensions.Timers.Listeners;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Triggers;

namespace Microsoft.Azure.WebJobs.Extensions.Timers.Bindings
{
    internal class TimerTriggerBinding : ITriggerBinding
    {
        private readonly ParameterInfo _parameter;
        private readonly TimerTriggerAttribute _attribute;
        private readonly TimersConfiguration _config;
        private IReadOnlyDictionary<string, Type> _bindingContract;

        public TimerTriggerBinding(ParameterInfo parameter, TimerTriggerAttribute attribute, TimersConfiguration config)
        {
            _attribute = attribute;
            _parameter = parameter;
            _config = config;
            _bindingContract = CreateBindingDataContract();
        }

        public Type TriggerValueType
        {
            get
            {
                return typeof(TimerInfo);
            }
        }

        public IReadOnlyDictionary<string, Type> BindingDataContract
        {
            get { return _bindingContract; }
        }

        public Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
        {
            TimerInfo timerInfo = value as TimerInfo;
            if (timerInfo == null)
            {
                timerInfo = new TimerInfo(_attribute.Schedule);
            }

            IValueProvider valueProvider = new ValueProvider(value, _parameter.ParameterType);
            IReadOnlyDictionary<string, object> bindingData = CreateBindingData(timerInfo);

            return Task.FromResult<ITriggerData>(new TriggerData(valueProvider, bindingData));
        }

        public Task<IListener> CreateListenerAsync(ListenerFactoryContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }
            return Task.FromResult<IListener>(new TimerListener(_attribute, context.Descriptor.Id, _config, context.Executor));
        }

        public ParameterDescriptor ToParameterDescriptor()
        {
            TimerTriggerParameterDescriptor descriptor = new TimerTriggerParameterDescriptor
            {
                Name = _parameter.Name,
                DisplayHints = new ParameterDisplayHints
                {
                    Prompt = "Enter an ISO DateTime string (yyyy-mm-ddThh:mm:ssZ)",
                    Description = string.Format("Timer executed on schedule ({0})", _attribute.Schedule.ToString()),
                    DefaultValue = DateTime.UtcNow.ToString("o")
                }
            };
            return descriptor;
        }

        private IReadOnlyDictionary<string, Type> CreateBindingDataContract()
        {
            Dictionary<string, Type> contract = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            contract.Add("TimerTrigger", typeof(string));

            return contract;
        }

        private IReadOnlyDictionary<string, object> CreateBindingData(TimerInfo timerInfo)
        {
            Dictionary<string, object> bindingData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            bindingData.Add("TimerTrigger", DateTime.UtcNow.ToString());

            // TODO: figure out if there is any binding data we need

            return bindingData;
        }

        private class ValueProvider : IValueProvider
        {
            private readonly object _value;
            private readonly Type _valueType;

            public ValueProvider(object value, Type valueType)
            {
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

        private class TimerTriggerParameterDescriptor : TriggerParameterDescriptor
        {
            public override string GetTriggerReason(IDictionary<string, string> arguments)
            {
                return string.Format("Timer fired at {0}", DateTime.UtcNow.ToString("o"));
            }
        }
    }
}
