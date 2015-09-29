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
    public class WebHooksConfiguration : IDisposable
    {
        private HttpConfiguration _httpConfiguration;
        private bool disposedValue = false;

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
        /// Gets the <see cref="HttpConfiguration"/> that will be used by <see cref="Microsoft.AspNet.WebHooks.WebHookReceiver"/>s.
        /// </summary>
        internal HttpConfiguration HttpConfiguration
        {
            get
            {
                if (_httpConfiguration == null)
                {
                    _httpConfiguration = new HttpConfiguration();
                }
                return _httpConfiguration;
            }
        }

        /// <summary>
        /// Adds the specified <see cref="WebHookReceiver"/> to the request pipeline.
        /// </summary>
        /// <typeparam name="T">The Type of <see cref="WebHookReceiver"/> to add.</typeparam>
        public void UseReceiver<T>() where T : WebHookReceiver
        {
            // Noop - just to ensure receiver assembly is loaded in to memory so it's
            // receivers can be discovered.
        }

        #region IDisposable Support
        /// <summary>
        /// Disposes the instance.
        /// </summary>
        /// <param name="disposing">Flag indicating whether we're being disposed.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (_httpConfiguration != null)
                    {
                        _httpConfiguration.Dispose();
                        _httpConfiguration = null;
                    }
                }

                disposedValue = true;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
