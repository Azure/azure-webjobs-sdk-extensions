// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

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
    }
}
