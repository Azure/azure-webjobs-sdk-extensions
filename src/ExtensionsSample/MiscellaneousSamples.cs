using System;
using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Extensions.Core;

namespace ExtensionsSample
{
    public static class MiscellaneousSamples
    {
        /// <summary>
        /// Demonstrates use of the ExecutionContext binding
        /// </summary>
        [NoAutomaticTrigger]
        public static void ExecutionContext(
            ExecutionContext context,
            TextWriter log)
        {
            string msg = string.Format("Invocation ID: {0}", context.InvocationId);
            Console.WriteLine(msg);
            log.WriteLine(msg);
        }
    }
}
