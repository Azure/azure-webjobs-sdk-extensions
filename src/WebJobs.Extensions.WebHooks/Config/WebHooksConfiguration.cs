// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.ObjectModel;
using Microsoft.AspNet.WebHooks;

namespace Microsoft.Azure.WebJobs.Extensions.WebHooks
{
    /// <summary>
    /// Configuration object for <see cref="WebHookTriggerAttribute"/> decorated job functions.
    /// </summary>
    public class WebHooksConfiguration
    {
        private Collection<WebHookReceiver> _receivers = new Collection<WebHookReceiver>();

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
        /// Gets the collection of <see cref="WebHookReceiver"/>s.
        /// </summary>
        internal Collection<WebHookReceiver> WebHookReceivers
        {
            get
            {
                return _receivers;
            }
        }

        /// <summary>
        /// Adds the specified <see cref="WebHookReceiver"/> to the request pipeline.
        /// </summary>
        /// <typeparam name="T">The Type of <see cref="WebHookReceiver"/> to add.</typeparam>
        public void UseReceiver<T>() where T : WebHookReceiver, new()
        {
            WebHookReceivers.Add(new T());
        }

        /// <summary>
        /// Adds the specified <see cref="WebHookReceiver"/> to the request pipeline.
        /// </summary>
        /// <typeparam name="T">The Type of <see cref="WebHookReceiver"/> to add.</typeparam>
        /// <param name="receiver">The <see cref="WebHookReceiver"/> instance to add.</param>
        public void UseReceiver<T>(T receiver) where T : WebHookReceiver
        {
            WebHookReceivers.Add(receiver);
        }
    }
}
