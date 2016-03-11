// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;

using SendGridMessage = SendGrid.SendGridMessage;

namespace Microsoft.Azure.WebJobs.Extensions.SendGrid
{
    internal class SendGridAttributeBindingProvider : IBindingProvider
    {
        private readonly SendGridConfiguration _config;
        private readonly INameResolver _nameResolver;

        public SendGridAttributeBindingProvider(SendGridConfiguration config, INameResolver nameResolver)
        {
            _config = config;
            _nameResolver = nameResolver;
        }

        public Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            ParameterInfo parameter = context.Parameter;
            SendGridAttribute attribute = parameter.GetCustomAttribute<SendGridAttribute>(inherit: false);
            if (attribute == null)
            {
                return Task.FromResult<IBinding>(null);
            }

            if (context.Parameter.ParameterType != typeof(SendGridMessage) &&
                context.Parameter.ParameterType != typeof(SendGridMessage).MakeByRefType())
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture,
                    "Can't bind SendGridAttribute to type '{0}'.", parameter.ParameterType));
            }

            if (string.IsNullOrEmpty(_config.ApiKey))
            {
                throw new InvalidOperationException(
                    string.Format("The SendGrid ApiKey must be set either via a '{0}' app setting, via a '{0}' environment variable, or directly in code via SendGridConfiguration.ApiKey.",
                    SendGridConfiguration.AzureWebJobsSendGridApiKeyName));
            }

            return Task.FromResult<IBinding>(new SendGridBinding(parameter, attribute, _config, _nameResolver, context));
        }
    }
}