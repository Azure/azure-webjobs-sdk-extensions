// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Triggers;

namespace Microsoft.Azure.WebJobs.Extensions.Timers.Bindings
{
    internal class TimerTriggerAttributeBindingProvider : ITriggerBindingProvider
    {
        private readonly TimersConfiguration _config;
        private readonly INameResolver _nameResolver;
        private readonly TraceWriter _trace;

        public TimerTriggerAttributeBindingProvider(TimersConfiguration config, INameResolver nameResolver, TraceWriter trace)
        {
            _config = config;
            _nameResolver = nameResolver;
            _trace = trace;
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

            TimerSchedule schedule = TimerSchedule.Create(timerTriggerAttribute, _nameResolver, _trace);

            return Task.FromResult<ITriggerBinding>(new TimerTriggerBinding(parameter, timerTriggerAttribute, schedule, _config, _trace));
        }
    }
}
