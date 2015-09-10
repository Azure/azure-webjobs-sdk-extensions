// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Timers.Config;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Triggers;

namespace Microsoft.Azure.WebJobs.Extensions.Timers.Bindings
{
    internal class TimerTriggerAttributeBindingProvider : ITriggerBindingProvider
    {
        private readonly TimersConfiguration _config;
        private readonly TraceWriter _trace;

        public TimerTriggerAttributeBindingProvider(TimersConfiguration config, TraceWriter trace)
        {
            _config = config;
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

            return Task.FromResult<ITriggerBinding>(new TimerTriggerBinding(parameter, timerTriggerAttribute, _config, _trace));
        }
    }
}
