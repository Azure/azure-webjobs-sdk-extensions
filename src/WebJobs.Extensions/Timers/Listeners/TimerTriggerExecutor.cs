using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;

namespace Microsoft.Azure.WebJobs.Extensions.Timers.Listeners
{
    internal class TimerTriggerExecutor
    {
        private readonly ITriggeredFunctionExecutor<TimerInfo> _innerExecutor;

        public TimerTriggerExecutor(ITriggeredFunctionExecutor<TimerInfo> innerExecutor)
        {
            _innerExecutor = innerExecutor;
        }

        public async Task<FunctionResult> ExecuteAsync(TimerInfo value, CancellationToken cancellationToken)
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
