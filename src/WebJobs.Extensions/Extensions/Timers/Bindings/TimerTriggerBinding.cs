// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Timers.Listeners;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.Timers.Bindings
{
    internal class TimerTriggerBinding : ITriggerBinding
    {
        private readonly ParameterInfo _parameter;
        private readonly TimerTriggerAttribute _attribute;
        private readonly TimerSchedule _schedule;
        private readonly TimersOptions _options;
        private readonly ILogger _logger;
        private readonly ScheduleMonitor _scheduleMonitor;
        private IReadOnlyDictionary<string, Type> _bindingContract;
        private string _timerName;

        public TimerTriggerBinding(ParameterInfo parameter, TimerTriggerAttribute attribute, TimerSchedule schedule, TimersOptions options, ILogger logger, ScheduleMonitor scheduleMonitor)
        {
            _attribute = attribute;
            _schedule = schedule;
            _parameter = parameter;
            _options = options;
            _logger = logger;
            _scheduleMonitor = scheduleMonitor;
            _bindingContract = CreateBindingDataContract();

            MethodInfo methodInfo = (MethodInfo)parameter.Member;
            _timerName = string.Format("{0}.{1}", methodInfo.DeclaringType.FullName, methodInfo.Name);
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

        public async Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
        {
            TimerInfo timerInfo = value as TimerInfo;
            if (timerInfo == null)
            {
                ScheduleStatus status = null;
                if (_attribute.UseMonitor && _scheduleMonitor != null)
                {
                    status = await _scheduleMonitor.GetStatusAsync(_timerName);
                }
                timerInfo = new TimerInfo(_schedule, status);
            }

            IValueProvider valueProvider = new ValueProvider(timerInfo);
            IReadOnlyDictionary<string, object> bindingData = CreateBindingData();

            return new TriggerData(valueProvider, bindingData);
        }

        public Task<IListener> CreateListenerAsync(ListenerFactoryContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            return Task.FromResult<IListener>(new TimerListener(_attribute, _schedule, _timerName, _options, context.Executor, _logger, _scheduleMonitor));
        }

        public ParameterDescriptor ToParameterDescriptor()
        {
            TimerTriggerParameterDescriptor descriptor = new TimerTriggerParameterDescriptor
            {
                Name = _parameter.Name,
                DisplayHints = new ParameterDisplayHints
                {
                    Description = string.Format("Timer executed on schedule ({0})", _schedule.ToString())
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

        private IReadOnlyDictionary<string, object> CreateBindingData()
        {
            Dictionary<string, object> bindingData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            bindingData.Add("TimerTrigger", DateTime.Now.ToString());

            return bindingData;
        }

        private class ValueProvider : IValueProvider
        {
            private readonly object _value;

            public ValueProvider(object value)
            {
                _value = value;
            }

            public Type Type
            {
                get { return typeof(TimerInfo); }
            }

            public Task<object> GetValueAsync()
            {
                return Task.FromResult(_value);
            }

            public string ToInvokeString()
            {
                return DateTime.Now.ToString("o");
            }
        }

        private class TimerTriggerParameterDescriptor : TriggerParameterDescriptor
        {
            public override string GetTriggerReason(IDictionary<string, string> arguments)
            {
                return string.Format("Timer fired at {0}", DateTime.Now.ToString("o"));
            }
        }
    }
}
