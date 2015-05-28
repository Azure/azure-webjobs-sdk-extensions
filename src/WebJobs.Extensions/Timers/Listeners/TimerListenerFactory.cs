using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Timers.Listeners;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;

namespace Microsoft.Azure.WebJobs.Extensions.Timers
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
