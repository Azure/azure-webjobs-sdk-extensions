// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

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
        public WebHookTriggerAttribute(string route = null)
        {
            Route = route;
        }

        /// <summary>
        /// Gets the route this function is triggered on.
        /// </summary>
        public string Route { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether values should come
        /// from Uri parameters or the request body, when binding to a
        /// user Type. By default, values come from the POST body.
        /// </summary>
        public bool FromUri { get; set; }
    }
}
