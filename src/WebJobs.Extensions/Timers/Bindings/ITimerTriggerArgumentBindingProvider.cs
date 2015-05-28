using System.Reflection;
using Microsoft.Azure.WebJobs.Host.Bindings;

namespace Microsoft.Azure.WebJobs.Extensions.Timers.Bindings
{
    internal interface ITimerTriggerArgumentBindingProvider
    {
        IArgumentBinding<TimerInfo> TryCreate(ParameterInfo parameter);
    }
}
