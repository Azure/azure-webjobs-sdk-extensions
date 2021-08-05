// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Logging.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.ApplicationInsights
{
    [Extension("ApplicationInsights")]
    internal class ApplicationInsightsExtensionConfigProvider : IExtensionConfigProvider
    {
        private readonly IConfiguration _configuration;
        private readonly IApplicationInsightsServiceFactory _applicationInsightsServiceFactory;
        private readonly INameResolver _nameResolver;
        private readonly ApplicationInsightsLoggerOptions _options;
        private readonly ILoggerFactory _loggerFactory;

        public ApplicationInsightsExtensionConfigProvider(
            IOptions<ApplicationInsightsLoggerOptions> options,
            IApplicationInsightsServiceFactory applicationInsightsServiceFactory,
            IConfiguration configuration,
            INameResolver nameResolver,
            ILoggerFactory loggerFactory)
        {
            _configuration = configuration;
            _applicationInsightsServiceFactory = applicationInsightsServiceFactory;
            _nameResolver = nameResolver;
            _options = options.Value;
            _loggerFactory = loggerFactory;
        }

        /// <inheritdoc />
        public void Initialize(ExtensionConfigContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            string appInsightsInstrumentationKey = _configuration["APPINSIGHTS_INSTRUMENTATIONKEY"];
            string appInsightsConnectionString = _configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];

            Console.WriteLine("*** CONNECTION STRING: " + appInsightsConnectionString + " ***");

            //_loggerFactory.AddApplicationInsights()
        }
    }
}
