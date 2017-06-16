// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading.Tasks.Dataflow;
using Microsoft.Azure.WebJobs.Host.Config;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.Http
{
    /// <summary>
    /// Defines the configuration options for the Http binding.
    /// </summary>
    public class HttpExtensionConfiguration : IExtensionConfigProvider
    {
        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        public HttpExtensionConfiguration()
        {
            MaxOutstandingRequests = DataflowBlockOptions.Unbounded;
            MaxConcurrentRequests = DataflowBlockOptions.Unbounded;
            RoutePrefix = HttpExtensionConstants.DefaultRoutePrefix;
        }

        /// <summary>
        /// Gets or sets the default route prefix that will be applied to
        /// function routes.
        /// </summary>
        public string RoutePrefix { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of outstanding requests that
        /// will be held at any given time. This limit includes requests
        /// that have started executing, as well as requests that have
        /// not yet started executing.
        /// If this limit is exceeded, new requests will be rejected with a 429 status code.
        /// </summary>
        public int MaxOutstandingRequests { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of http functions that will
        /// be allowed to execute in parallel.
        /// </summary>
        public int MaxConcurrentRequests { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether dynamic host counter
        /// checks should be enabled.
        /// </summary>
        public bool DynamicThrottlesEnabled { get; set; }

        /// <summary>
        /// Hook to enable a host to receive the response
        /// </summary>
        [JsonIgnore]
        public Action<HttpRequestMessage, object> SetResponse { get; set; }

        /// <summary>
        /// Initializes the extension.
        /// </summary>
        /// <param name="context">The <see cref="ExtensionConfigContext"/>.</param>
        public void Initialize(ExtensionConfigContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            context.Config.RegisterBindingExtension(new HttpTriggerAttributeBindingProvider(this.SetResponse));
            context.Config.RegisterBindingExtension(new HttpDirectRequestBindingProvider());
            context.Config.RegisterBindingExtensions(
                new ClaimsIdentityBindingProvider(),
                new ClaimsPrincipalBindingProvider());
        }
    }
}
