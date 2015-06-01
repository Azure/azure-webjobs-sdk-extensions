using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Timers.Config;
using Microsoft.Azure.WebJobs.Extensions.Timers.Converters;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Converters;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Triggers;

namespace Microsoft.Azure.WebJobs.Extensions.Timers.Bindings
{
    internal class TimerTriggerBinding : ITriggerBinding<TimerInfo>
    {
        private readonly string _parameterName;
        private readonly IObjectToTypeConverter<TimerInfo> _converter;
        private readonly IArgumentBinding<TimerInfo> _argumentBinding;
        private readonly TimerTriggerAttribute _attribute;
        private readonly TimersConfiguration _config;
        private IReadOnlyDictionary<string, Type> _bindingContract;

        public TimerTriggerBinding(string parameterName, Type parameterType, IArgumentBinding<TimerInfo> argumentBinding, TimerTriggerAttribute attribute, TimersConfiguration config)
        {
            _parameterName = parameterName;
            _converter = CreateConverter(parameterType);
            _argumentBinding = argumentBinding;
            _attribute = attribute;
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

        public async Task<ITriggerData> BindAsync(TimerInfo value, ValueBindingContext context)
        {
            IValueProvider valueProvider = await _argumentBinding.BindAsync(value, context);
            IReadOnlyDictionary<string, object> bindingData = CreateBindingData(value);

            return new TriggerData(valueProvider, bindingData);
        }

        public Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
        {
            TimerInfo message = null;

            if (!_converter.TryConvert(value, out message))
            {
                throw new InvalidOperationException("Unable to convert trigger to TimerInfo.");
            }

            return BindAsync(message, context);
        }

        public IListenerFactory CreateListenerFactory(FunctionDescriptor descriptor, ITriggeredFunctionExecutor<TimerInfo> executor)
        {
            if (descriptor == null)
            {
                throw new ArgumentNullException("descriptor");
            }
            string timerName = descriptor.Id;
            return new TimerListenerFactory(_attribute, timerName, _config, executor);
        }

        public IListenerFactory CreateListenerFactory(FunctionDescriptor descriptor, ITriggeredFunctionExecutor executor)
        {
            return CreateListenerFactory(descriptor, (ITriggeredFunctionExecutor<TimerInfo>)executor);
        }

        public ParameterDescriptor ToParameterDescriptor()
        {
            return new TimerTriggerParameterDescriptor
            {
                // TODO: Figure out Display Hints
            };
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
            bindingData.Add("TimerTrigger", DateTime.Now.ToString());

            // TODO: figure out if there is any binding data we need

            return bindingData;
        }

        private static IObjectToTypeConverter<TimerInfo> CreateConverter(Type parameterType)
        {
            return new CompositeObjectToTypeConverter<TimerInfo>(
                    new TimerInfoOutputConverter<TimerInfo>(new IdentityConverter<TimerInfo>()));
        }
    }
}
