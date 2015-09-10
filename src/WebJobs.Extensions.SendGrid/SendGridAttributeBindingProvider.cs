// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Framework;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Protocols;
using SendGrid;

namespace Microsoft.Azure.WebJobs.Extensions
{
    internal class SendGridAttributeBindingProvider : IBindingProvider
    {
        private readonly SendGridConfiguration _config;

        public SendGridAttributeBindingProvider(SendGridConfiguration config)
        {
            _config = config;
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

            if (context.Parameter.ParameterType != typeof(SendGrid.SendGridMessage))
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

            return Task.FromResult<IBinding>(new SendGridBinding(parameter, attribute, _config, context));
        }

        internal class SendGridBinding : IBinding
        {
            private readonly ParameterInfo _parameter;
            private readonly SendGridAttribute _attribute;
            private readonly SendGridConfiguration _config;
            private readonly Web _sendGrid;
            private readonly BindablePath _toFieldBinding;
            private readonly BindablePath _subjectFieldBinding;
            private readonly BindablePath _textFieldBinding;

            public SendGridBinding(ParameterInfo parameter, SendGridAttribute attribute, SendGridConfiguration config, BindingProviderContext context)
            {
                _parameter = parameter;
                _attribute = attribute;
                _config = config;

                _sendGrid = new Web(_config.ApiKey);

                if (!string.IsNullOrEmpty(_attribute.To))
                {
                    _toFieldBinding = new BindablePath(_attribute.To);
                    _toFieldBinding.ValidateContractCompatibility(context.BindingDataContract);
                }

                if (!string.IsNullOrEmpty(_attribute.Subject))
                {
                    _subjectFieldBinding = new BindablePath(_attribute.Subject);
                    _subjectFieldBinding.ValidateContractCompatibility(context.BindingDataContract);
                }

                if (!string.IsNullOrEmpty(_attribute.Text))
                {
                    _textFieldBinding = new BindablePath(_attribute.Text);
                    _textFieldBinding.ValidateContractCompatibility(context.BindingDataContract);
                }
            }

            public bool FromAttribute
            {
                get { return true; }
            }

            public async Task<IValueProvider> BindAsync(BindingContext context)
            {
                SendGridMessage message = CreateDefaultMessage(context.BindingData);

                return await BindAsync(message, context.ValueContext);
            }

            public Task<IValueProvider> BindAsync(object value, ValueBindingContext context)
            {
                SendGridMessage message = (SendGridMessage)value;

                return Task.FromResult<IValueProvider>(new SendGridValueBinder(_sendGrid, message));
            }

            public ParameterDescriptor ToParameterDescriptor()
            {
                return new ParameterDescriptor
                {
                    Name = _parameter.Name
                };
            }

            internal SendGridMessage CreateDefaultMessage(IReadOnlyDictionary<string, object> bindingData)
            {
                SendGridMessage message = new SendGridMessage();

                if (_config.FromAddress != null)
                {
                    message.From = _config.FromAddress;
                }

                if (_toFieldBinding != null)
                {
                    message.AddTo(_toFieldBinding.Bind(bindingData));
                }
                else
                {
                    if (!string.IsNullOrEmpty(_config.ToAddress))
                    {
                        message.AddTo(_config.ToAddress);
                    }
                }

                if (_subjectFieldBinding != null)
                {
                    message.Subject = _subjectFieldBinding.Bind(bindingData);
                }

                if (_textFieldBinding != null)
                {
                    message.Text = _textFieldBinding.Bind(bindingData);
                }

                return message;
            }

            internal class SendGridValueBinder : IValueBinder
            {
                private readonly SendGridMessage _message;
                private readonly Web _sendGrid;

                public SendGridValueBinder(Web sendGrid, SendGridMessage message)
                {
                    _message = message;
                    _sendGrid = sendGrid;
                }

                public Type Type
                {
                    get
                    {
                        return typeof(SendGridMessage);
                    }
                }

                public object GetValue()
                {
                    return _message;
                }

                public async Task SetValueAsync(object value, CancellationToken cancellationToken)
                {
                    if (_message.To == null || _message.To.Length == 0)
                    {
                        throw new InvalidOperationException("A 'To' address must be specified for the message.");
                    }
                    if (_message.From == null || string.IsNullOrEmpty(_message.From.Address))
                    {
                        throw new InvalidOperationException("A 'From' address must be specified for the message.");
                    }

                    await _sendGrid.DeliverAsync(_message);
                }

                public string ToInvokeString()
                {
                    return null;
                }
            }
        }
    }
}
