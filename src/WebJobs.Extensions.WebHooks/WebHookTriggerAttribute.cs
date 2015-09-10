using System;

namespace Microsoft.Azure.WebJobs.Extensions.WebHooks
{
    /// <summary>
    /// Trigger attribute used to declare that a job function should be invoked
    /// when WebHook HTTP messages are posted to the configured address.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class WebHookTriggerAttribute : Attribute
    {
        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="route">Optional override of the default route the function will
        /// be triggered on.</param>
        public WebHookTriggerAttribute(string route = null)
        {
            Route = route;
        }

        /// <summary>
        /// Gets the route this function is triggered on.
        /// </summary>
        public string Route { get; private set; }
    }
}
