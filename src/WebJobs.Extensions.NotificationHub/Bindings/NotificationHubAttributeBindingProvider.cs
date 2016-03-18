// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;

namespace Microsoft.Azure.WebJobs.Extensions.NotificationHub
{
    internal class NotificationHubAttributeBindingProvider : IBindingProvider
    {
        private readonly INameResolver _nameResolver;
        private readonly IConverterManager _converterManager;
        private NotificationHubClientService _clientService;

        public NotificationHubAttributeBindingProvider(INameResolver nameResolver, IConverterManager converterManager, NotificationHubConfiguration config)
        {
            _nameResolver = nameResolver;
            _converterManager = converterManager;
            _clientService = new NotificationHubClientService(config);
        }

        public Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            ParameterInfo parameter = context.Parameter;
            NotificationHubAttribute attribute = parameter.GetCustomAttribute<NotificationHubAttribute>(inherit: false);
            if (attribute == null)
            {
                return Task.FromResult<IBinding>(null);
            }
            Func<string, NotificationHubClientService> invokeStringBinder = (invokeString) => _clientService;
            IBinding binding = BindingFactory.BindCollector(
                parameter,
                _converterManager,
                (nhClientService, valueBindingContext) => new NotificationHubAsyncCollector(_clientService, attribute.TagExpression),
                "NotificationHubs",
                invokeStringBinder);
            return Task.FromResult(binding);
        }
    }
}
