// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.ObjectModel;
using System.Reflection;
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
            var sendGridConfig = SendGridHelpers.CreateConfiguration(Metadata);
            if (!string.IsNullOrEmpty(sendGridConfig.ApiKey))
            {
                Config.UseSendGrid(sendGridConfig);
            }
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
