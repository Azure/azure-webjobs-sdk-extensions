// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Framework;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Protocols;
using SendGrid;

namespace Microsoft.Azure.WebJobs.Extensions.SendGrid
{
    internal class SendGridBinding : IBinding
    {
        private readonly ParameterInfo _parameter;
        private readonly SendGridAttribute _attribute;
        private readonly SendGridConfiguration _config;
        private readonly INameResolver _nameResolver;
        private readonly Web _sendGrid;
        private readonly BindablePath _toFieldBinding;
        private readonly BindablePath _fromFieldBinding;
        private readonly BindablePath _subjectFieldBinding;
        private readonly BindablePath _textFieldBinding;

        public SendGridBinding(ParameterInfo parameter, SendGridAttribute attribute, SendGridConfiguration config, INameResolver nameResolver, BindingProviderContext context)
        {
            _parameter = parameter;
            _attribute = attribute;
            _config = config;
            _nameResolver = nameResolver;

            _sendGrid = new Web(_config.ApiKey);

            if (!string.IsNullOrEmpty(_attribute.To))
            {
                _toFieldBinding = CreateBindablePath(_attribute.To, context.BindingDataContract);
            }

            if (!string.IsNullOrEmpty(_attribute.From))
            {
                _fromFieldBinding = CreateBindablePath(_attribute.From, context.BindingDataContract);
            }

            if (!string.IsNullOrEmpty(_attribute.Subject))
            {
                _subjectFieldBinding = CreateBindablePath(_attribute.Subject, context.BindingDataContract);
            }

            if (!string.IsNullOrEmpty(_attribute.Text))
            {
                _textFieldBinding = CreateBindablePath(_attribute.Text, context.BindingDataContract);
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

            if (_fromFieldBinding != null)
            {
                MailAddress fromAddress = null;
                string boundFromAddress = _fromFieldBinding.Bind(bindingData);
                if (!ParseFromAddress(boundFromAddress, out fromAddress))
                {
                    throw new ArgumentException("Invalid 'From' address specified");
                }
                message.From = fromAddress;
            }
            else
            {
                if (_config.FromAddress != null)
                {
                    message.From = _config.FromAddress;
                }
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

        private BindablePath CreateBindablePath(string pattern, IReadOnlyDictionary<string, Type> bindingDataContract)
        {
            if (_nameResolver != null)
            {
                pattern = _nameResolver.ResolveWholeString(pattern);
            }
            BindablePath bindablePath = new BindablePath(pattern);
            bindablePath.ValidateContractCompatibility(bindingDataContract);

            return bindablePath;
        }

        internal static bool ParseFromAddress(string from, out MailAddress fromAddress)
        {
            fromAddress = null;

            int idx = from.IndexOf('@');
            if (idx < 0)
            {
                return false;
            }

            idx = from.IndexOf(':', idx);
            if (idx > 0)
            {
                string address = from.Substring(0, idx);
                string displayName = from.Substring(idx + 1);
                fromAddress = new MailAddress(address, displayName);
                return true;
            }
            else
            {
                fromAddress = new MailAddress(from);
                return true;
            }
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
                if (value == null)
                {
                    // if this is a 'ref' binding and the user set the parameter to null, that
                    // signals that they don't want us to send the message
                    return;
                }

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
