using System;
using System.IO;
using Microsoft.Azure.WebJobs.Extensions.Extensions.Core;
using Microsoft.Azure.WebJobs.Extensions.Timers;

namespace ExtensionsSample
{
    public static class MiscellaneousSamples
    {
        /// <summary>
        /// Demonstrates use of the ExecutionContext binding
        /// </summary>
        public static void ExecutionContext(
            [TimerTrigger("*/10 * * * * *")] TimerInfo timerInfo, 
            ExecutionContext context,
            TextWriter log)
        {
            string msg = string.Format("Invocation ID: {0}", context.InvocationId);
            Console.WriteLine(msg);
            log.WriteLine(msg);
        }
    }
}
