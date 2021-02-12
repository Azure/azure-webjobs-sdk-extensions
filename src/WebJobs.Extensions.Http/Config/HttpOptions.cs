// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks.Dataflow;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Hosting;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.Http
{
    /// <summary>
    /// Defines the configuration options for the Http binding.
    /// </summary>
    public class HttpOptions : IOptionsFormatter
    {
        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        public HttpOptions()
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
        /// Gets or sets a value indicating whether chunked 
        /// transfer should be enabled.
        /// </summary>
        public bool ChunkedTransferEnabled { get; set; }

        /// <summary>
        /// Gets or sets the Action used to receive the response.
        /// </summary>
        [JsonIgnore]
        public Action<HttpRequest, object> SetResponse { get; set; }

        public string Format()
        {
            StringWriter sw = new StringWriter();
            using (JsonTextWriter writer = new JsonTextWriter(sw) { Formatting = Formatting.Indented })
            {
                writer.WriteStartObject();

                writer.WritePropertyName(nameof(DynamicThrottlesEnabled));
                writer.WriteValue(DynamicThrottlesEnabled);

                writer.WritePropertyName(nameof(ChunkedTransferEnabled));
                writer.WriteValue(ChunkedTransferEnabled);

                writer.WritePropertyName(nameof(MaxConcurrentRequests));
                writer.WriteValue(MaxConcurrentRequests);

                writer.WritePropertyName(nameof(MaxOutstandingRequests));
                writer.WriteValue(MaxOutstandingRequests);

                writer.WritePropertyName(nameof(RoutePrefix));
                writer.WriteValue(RoutePrefix);

                writer.WriteEndObject();
            }

            return sw.ToString();
        }
    }
}
