// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Bindings;
using Microsoft.Azure.WebJobs.Extensions.Core.Listener;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Triggers;

namespace Microsoft.Azure.WebJobs.Extensions.Core
{
    internal class ErrorTriggerAttributeBindingProvider : ITriggerBindingProvider
    {
        private readonly JobHostConfiguration _config;

        public ErrorTriggerAttributeBindingProvider(JobHostConfiguration config)
        {
            _config = config;
        }

        public Task<ITriggerBinding> TryCreateAsync(TriggerBindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            ParameterInfo parameter = context.Parameter;
            ErrorTriggerAttribute attribute = parameter.GetCustomAttribute<ErrorTriggerAttribute>(inherit: false);
            if (attribute == null)
            {
                return Task.FromResult<ITriggerBinding>(null);
            }

            if (parameter.ParameterType != typeof(TraceFilter) &&
                parameter.ParameterType != typeof(TraceEvent) &&
                parameter.ParameterType != typeof(IEnumerable<TraceEvent>))
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture,
                    "Can't bind ErrorTriggerAttribute to type '{0}'.", parameter.ParameterType));
            }

            return Task.FromResult<ITriggerBinding>(new ErrorTriggerBinding(_config, context.Parameter));
        }

        private class ErrorTriggerBinding : ITriggerBinding
        {
            private readonly JobHostConfiguration _config;
            private readonly ParameterInfo _parameter;
            private readonly IReadOnlyDictionary<string, Type> _bindingContract;

            public ErrorTriggerBinding(JobHostConfiguration config, ParameterInfo parameter)
            {
                _config = config;
                _parameter = parameter;
                _bindingContract = CreateBindingDataContract();
            }

            public IReadOnlyDictionary<string, Type> BindingDataContract
            {
                get { return _bindingContract; }
            }

            public Type TriggerValueType
            {
                get { return typeof(TraceFilter); }
            }

            public Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
            {
                if (value != null && value.GetType() == typeof(string))
                {
                    throw new NotSupportedException("ErrorTrigger does not support Dashboard invocation.");
                }

                TraceFilter triggerValue = (TraceFilter)value;
                IValueBinder valueBinder = new ErrorValueBinder(_parameter, triggerValue);
                TriggerData triggerData = new TriggerData(valueBinder, GetBindingData(triggerValue));

                return Task.FromResult<ITriggerData>(triggerData);
            }

            public Task<IListener> CreateListenerAsync(ListenerFactoryContext context)
            {
                if (context == null)
                {
                    throw new ArgumentNullException("context");
                }

                return Task.FromResult<IListener>(new ErrorTriggerListener(_config, _parameter, context.Executor));
            }

            public ParameterDescriptor ToParameterDescriptor()
            {
                return new SampleTriggerParameterDescriptor
                {
                    Name = _parameter.Name,
                    DisplayHints = new ParameterDisplayHints
                    {
                        Prompt = "Error Info"
                    }
                };
            }

            private IReadOnlyDictionary<string, object> GetBindingData(TraceFilter value)
            {
                Dictionary<string, object> bindingData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                bindingData.Add("ErrorTrigger", value);
                bindingData.Add("Message", value.Message);

                return bindingData;
            }

            private IReadOnlyDictionary<string, Type> CreateBindingDataContract()
            {
                Dictionary<string, Type> contract = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
                contract.Add("Message", typeof(string));

                return contract;
            }

            private class SampleTriggerParameterDescriptor : TriggerParameterDescriptor
            {
                public override string GetTriggerReason(IDictionary<string, string> arguments)
                {
                    return string.Format("Error trigger fired");
                }
            }

            private class ErrorValueBinder : ValueBinder
            {
                private readonly ParameterInfo _parameter;
                private readonly TraceFilter _value;

                public ErrorValueBinder(ParameterInfo parameter, TraceFilter value)
                    : base(parameter.ParameterType)
                {
                    _parameter = parameter;
                    _value = value;
                }

                public override Task<object> GetValueAsync()
                {
                    if (_parameter.ParameterType == typeof(TraceEvent))
                    {
                        return Task.FromResult<object>(_value.GetEvents().Last());
                    }
                    else if (_parameter.ParameterType == typeof(IEnumerable<TraceEvent>))
                    {
                        return Task.FromResult<object>(_value.GetEvents().AsEnumerable());
                    }
                    return Task.FromResult<object>(_value);
                }

                public override string ToInvokeString()
                {
                    return _value.Message;
                }
            }
        }
    }
}
