// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Extensions.Http
{
    /// <summary>
    /// Defines the configuration options for the Http binding.
    /// </summary>
    [Extension("HTTP")]
    internal class HttpExtensionConfigProvider : IExtensionConfigProvider
    {
        private readonly IOptions<HttpOptions> _options;

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        public HttpExtensionConfigProvider(IOptions<HttpOptions> options)
        {
            _options = options;
        }

        public void Initialize(ExtensionConfigContext context)
        {
            var httpBindingProvider = new HttpTriggerAttributeBindingProvider(_options.Value.SetResponse);
            context.AddBindingRule<HttpTriggerAttribute>()
                .BindToTrigger(httpBindingProvider);
        }
    }
}
