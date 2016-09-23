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
            Config.UseSendGrid(sendGridConfig);
        }

        /// <inheritdoc/>
        public override bool TryResolveAssembly(string assemblyName, out Assembly assembly)
        {
            assembly = null;

            Assembly sendGridAssembly = typeof(SendGridAPIClient).Assembly;
            if (string.Compare(assemblyName, sendGridAssembly.GetName().Name, StringComparison.OrdinalIgnoreCase) == 0)
            {
                assembly = sendGridAssembly;
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
                        ApiKey = Context.GetMetadataValue<string>("apiKey"),
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
