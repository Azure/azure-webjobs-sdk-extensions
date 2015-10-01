// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Web.Http;
using Microsoft.AspNet.WebHooks;

namespace Microsoft.Azure.WebJobs.Extensions.WebHooks
{
    /// <summary>
    /// Configuration object for <see cref="WebHookTriggerAttribute"/> decorated job functions.
    /// </summary>
    public class WebHooksConfiguration
    {
        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        public WebHooksConfiguration()
        {
            string value = Environment.GetEnvironmentVariable("WEBJOBS_PORT") ?? "65000";
            Port = int.Parse(value);
        }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="port">The port that the host
        /// should listen for WebHook invocations on.</param>
        public WebHooksConfiguration(int port)
        {
            Port = port;
        }

        /// <summary>
        /// Gets the listen port that the host is listening for WebHook invocations on.
        /// </summary>
        public int Port { get; private set; }

        /// <summary>
        /// Adds the specified <see cref="WebHookReceiver"/> to the request pipeline.
        /// <remarks>
        /// Once a receiver is registered, any WebHook functions with a route starting
        /// with that receiver name will be authenticated by that receiver.
        /// </remarks>
        /// </summary>
        /// <typeparam name="T">The Type of <see cref="WebHookReceiver"/> to add.</typeparam>
        public void UseReceiver<T>() where T : WebHookReceiver
        {
            // Noop - just to ensure receiver assembly is loaded in to memory so it's
            // receivers can be discovered.
        }
    }
}
