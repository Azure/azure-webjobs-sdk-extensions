using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using WebJobs.Extensions.Files;

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

        public async Task<IListener> CreateAsync(ListenerFactoryContext context)
        {
            TimerTriggerExecutor triggerExecutor = new TimerTriggerExecutor(_executor);
            return new WebJobs.Extensions.Timers.Listeners.TimerListener(_attribute, triggerExecutor);
        }
    }

    internal class TimerTriggerExecutor
    {
        private readonly ITriggeredFunctionExecutor<TimerInfo> _innerExecutor;

        public TimerTriggerExecutor(ITriggeredFunctionExecutor<TimerInfo> innerExecutor)
        {
            _innerExecutor = innerExecutor;
        }

        public async Task<bool> ExecuteAsync(TimerInfo value, CancellationToken cancellationToken)
        {
            TriggeredFunctionData<TimerInfo> input = new TriggeredFunctionData<TimerInfo>
            {
                // TODO: how to set this properly?
                ParentId = null,
                TriggerValue = value
            };

            return await _innerExecutor.TryExecuteAsync(input, cancellationToken);
        }
    }
}
