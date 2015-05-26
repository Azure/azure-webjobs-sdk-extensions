using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using WebJobs.Extensions.Files;
using WebJobs.Extensions.Timers.Listeners;

namespace WebJobs.Extensions.Timers
{
    internal class TimerListenerFactory : IListenerFactory
    {
        private readonly TimerTriggerAttribute _attribute;
        private readonly ITriggeredFunctionExecutor<TimerInfo> _executor;

        public TimerListenerFactory(TimerTriggerAttribute attribute, ITriggeredFunctionExecutor<TimerInfo> executor)
        {
            _attribute = attribute;
            _executor = executor;
        }

        public Task<IListener> CreateAsync(ListenerFactoryContext context)
        {
            TimerTriggerExecutor triggerExecutor = new TimerTriggerExecutor(_executor);
            TimerListener listener = new TimerListener(_attribute, triggerExecutor);
            return Task.FromResult<IListener>(listener);
        }
    }
}
