// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.Timers.Bindings
{
    internal class TimerTriggerAttributeBindingProvider : ITriggerBindingProvider
    {
        private readonly TimersOptions _options;
        private readonly INameResolver _nameResolver;
        private readonly ILogger _logger;
        private readonly ScheduleMonitor _scheduleMonitor;
        private readonly IDrainModeManager _drainModeManager;

        public TimerTriggerAttributeBindingProvider(TimersOptions options, INameResolver nameResolver, ILogger logger, ScheduleMonitor scheduleMonitor, IDrainModeManager drainModeManager)
        {
            _options = options;
            _nameResolver = nameResolver;
            _logger = logger;
            _scheduleMonitor = scheduleMonitor;
            _drainModeManager = drainModeManager;
        }

        public Task<ITriggerBinding> TryCreateAsync(TriggerBindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            ParameterInfo parameter = context.Parameter;
            TimerTriggerAttribute timerTriggerAttribute = parameter.GetCustomAttribute<TimerTriggerAttribute>(inherit: false);

            if (timerTriggerAttribute == null)
            {
                return Task.FromResult<ITriggerBinding>(null);
            }

            if (parameter.ParameterType != typeof(TimerInfo))
            {
                throw new InvalidOperationException(string.Format("Can't bind TimerTriggerAttribute to type '{0}'.", parameter.ParameterType));
            }

            TimerSchedule schedule = TimerSchedule.Create(timerTriggerAttribute, _nameResolver, _logger);

            return Task.FromResult<ITriggerBinding>(new TimerTriggerBinding(parameter, timerTriggerAttribute, schedule, _options, _logger, _scheduleMonitor, _drainModeManager));
        }
    }
}
