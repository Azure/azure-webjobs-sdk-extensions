// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.ObjectModel;
using System.Net.Mail;
using System.Reflection;
using Microsoft.Azure.WebJobs.Extensions.SendGrid;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Script.Extensibility;
using Newtonsoft.Json.Linq;
using SendGrid;

namespace Microsoft.Azure.WebJobs.Extensions.Bindings
{
    /// <summary>
    /// BindingProvider for SendGrid extensions
    /// </summary>
    public class SendGridScriptBindingProvider : ScriptBindingProvider
    {
        /// <inheritdoc/>
        public SendGridScriptBindingProvider(JobHostConfiguration config, JObject hostMetadata, TraceWriter traceWriter) 
            : base(config, hostMetadata, traceWriter)
        {
        }

        /// <inheritdoc/>
        public override bool TryCreate(ScriptBindingContext context, out ScriptBinding binding)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            binding = null;

            if (string.Compare(context.Type, "sendGrid", StringComparison.OrdinalIgnoreCase) == 0)
            {
                binding = new SendGridBinding(context);
            }

            return binding != null;
        }

        /// <inheritdoc/>
        public override void Initialize()
        {
            SendGridConfiguration sendGridConfig = CreateConfiguration(Metadata);
            Config.UseSendGrid(sendGridConfig);
        }

        /// <inheritdoc/>
        public override bool TryResolveAssembly(string assemblyName, out Assembly assembly)
        {
            assembly = null;

            if (string.Compare(assemblyName, "SendGridMail", StringComparison.OrdinalIgnoreCase) == 0)
            {
                assembly = typeof(SendGridMessage).Assembly;
            }

            return assembly != null;
        }

        internal static SendGridConfiguration CreateConfiguration(JObject metadata)
        {
            SendGridConfiguration sendGridConfig = new SendGridConfiguration();

            JObject configSection = (JObject)metadata.GetValue("sendGrid", StringComparison.OrdinalIgnoreCase);
            JToken value = null;
            if (configSection != null)
            {
                MailAddress mailAddress = null;
                if (configSection.TryGetValue("fromAddress", StringComparison.OrdinalIgnoreCase, out value) &&
                    SendGridHelpers.TryParseAddress((string)value, out mailAddress))
                {
                    sendGridConfig.FromAddress = mailAddress;
                }

                if (configSection.TryGetValue("toAddress", StringComparison.OrdinalIgnoreCase, out value) &&
                    SendGridHelpers.TryParseAddress((string)value, out mailAddress))
                {
                    sendGridConfig.ToAddress = mailAddress;
                }
            }

            return sendGridConfig;
        }

        private class SendGridBinding : ScriptBinding
        {
            public SendGridBinding(ScriptBindingContext context) : base(context)
            {
            }

            public override Type DefaultType
            {
                get
                {
                    return typeof(IAsyncCollector<JObject>);
                }
            }

            public override Collection<Attribute> GetAttributes()
            {
                return new Collection<Attribute>
                {
                    new SendGridAttribute
                    {
                        To = Context.GetMetadataValue<string>("to"),
                        From = Context.GetMetadataValue<string>("from"),
                        Subject = Context.GetMetadataValue<string>("subject"),
                        Text = Context.GetMetadataValue<string>("text")
                    }
                };
            }
        }
    }
}
