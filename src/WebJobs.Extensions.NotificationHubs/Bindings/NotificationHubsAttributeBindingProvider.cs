// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;

namespace Microsoft.Azure.WebJobs.Extensions.NotificationHubs
{
    internal class NotificationHubsAttributeBindingProvider : IBindingProvider
    {
        private readonly INameResolver _nameResolver;
        private readonly IConverterManager _converterManager;
        private NotificationHubsConfiguration _nhClientConfig;
        private NotificationHubClientService _nhClientService;

        public NotificationHubsAttributeBindingProvider(INameResolver nameResolver, IConverterManager converterManager, NotificationHubsConfiguration config)
        {
            _nameResolver = nameResolver;
            _converterManager = converterManager;
            _nhClientConfig = config;
            _nhClientService = new NotificationHubClientService(config);
        }

        public Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            ParameterInfo parameter = context.Parameter;
            NotificationHubsAttribute attribute = parameter.GetCustomAttribute<NotificationHubsAttribute>(inherit: false);
            if (attribute == null)
            {
                return Task.FromResult<IBinding>(null);
            }
            Func<string, NotificationHubClientService> invokeStringBinder = (invokeString) => _nhClientService;
            IBinding binding = BindingFactory.BindCollector(
                parameter,
                _converterManager,
                (nhClientService, valueBindingContext) => new NotificationHubsAsyncCollector(_nhClientService, attribute.TagExpression),
                "NotificationHubs",
                invokeStringBinder);
            return Task.FromResult(binding);
        }
    }
}
