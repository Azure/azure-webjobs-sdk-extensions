// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Extensions.WebHooks;

namespace Microsoft.Azure.WebJobs
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
        /// Constructs a new instance.
        /// </summary>
        /// <param name="receiver">The WebHook Receiver to use for this WebHook. This should be a receiver that
        /// has been registered on startup using <see cref="WebHooksConfiguration.UseReceiver{T}()"/>.</param>
        /// <param name="receiverId">Optional WebHook Id.</param>
        public WebHookTriggerAttribute(string receiver, string receiverId = null)
        {
            if (string.IsNullOrEmpty(receiver))
            {
                throw new ArgumentNullException("receiver");
            }

            Receiver = receiver.ToLowerInvariant();

            if (!string.IsNullOrEmpty(receiverId))
            {
                ReceiverId = receiverId.ToLowerInvariant();
                Route = string.Format("{0}/{1}", Receiver, ReceiverId);
            }
            else
            {
                Route = Receiver;
            }
        }

        /// <summary>
        /// Gets the WebHook Receiver to use for this WebHook.
        /// </summary>
        public string Receiver { get; private set; }

        /// <summary>
        /// Gets the WebHook Receiver Id to use for this WebHook.
        /// </summary>
        public string ReceiverId { get; private set; }

        /// <summary>
        /// Gets the WebHook route the function will be triggered on.
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
