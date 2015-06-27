using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;

namespace Microsoft.Azure.WebJobs.Extensions.Timers.Listeners
{
    internal class TimerTriggerExecutor
    {
        private readonly ITriggeredFunctionExecutor _innerExecutor;

        public TimerTriggerExecutor(ITriggeredFunctionExecutor innerExecutor)
        {
            _innerExecutor = innerExecutor;
        }

        public async Task<FunctionResult> ExecuteAsync(TimerInfo value, CancellationToken cancellationToken)
        {
            TriggeredFunctionData input = new TriggeredFunctionData
            {
                // TODO: how to set this properly?
                ParentId = null,
                TriggerValue = value
            };

            return await _innerExecutor.TryExecuteAsync(input, cancellationToken);
        }
    }
}
