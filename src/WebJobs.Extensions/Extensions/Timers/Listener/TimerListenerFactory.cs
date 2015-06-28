using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Timers.Config;
using Microsoft.Azure.WebJobs.Extensions.Timers.Listeners;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;

namespace Microsoft.Azure.WebJobs.Extensions.Timers
{
    internal class TimerListenerFactory : IListenerFactory
    {
        private readonly TimerTriggerAttribute _attribute;
        private readonly TimersConfiguration _config;
        private readonly ITriggeredFunctionExecutor _executor;
        private readonly string _timerName;

        public TimerListenerFactory(TimerTriggerAttribute attribute, string timerName, TimersConfiguration config, ITriggeredFunctionExecutor executor)
        {
            _attribute = attribute;
            _timerName = timerName;
            _config = config;
            _executor = executor;
        }

        public Task<IListener> CreateAsync(ListenerFactoryContext context)
        {
            TimerListener listener = new TimerListener(_attribute, _timerName, _config, _executor);
            return Task.FromResult<IListener>(listener);
        }
    }
}
