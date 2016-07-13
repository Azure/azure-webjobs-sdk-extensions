// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.ObjectModel;
using System.Reflection;
using Microsoft.Azure.WebJobs.Extensions.Twilio;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Script.Extensibility;
using Newtonsoft.Json.Linq;
using Twilio;

namespace Microsoft.Azure.WebJobs.Extensions.Bindings
{
    /// <summary>
    /// BindingProvider for Twilio extensions
    /// </summary>
    public class TwilioScriptBindingProvider : ScriptBindingProvider
    {
        private readonly string _twilioAssemblyName;

        /// <inheritdoc/>
        public TwilioScriptBindingProvider(JobHostConfiguration config, JObject hostMetadata, TraceWriter traceWriter) 
            : base(config, hostMetadata, traceWriter)
        {
            _twilioAssemblyName = typeof(SMSMessage).Assembly.GetName().Name;
        }

        internal static TwilioSmsConfiguration CreateConfiguration(JObject metadata)
        {
            var twilioConfig = new TwilioSmsConfiguration();

            JObject configSection = (JObject)metadata.GetValue("twilioSMS", StringComparison.OrdinalIgnoreCase);
            JToken value = null;
            if (configSection != null)
            {
                if (configSection.TryGetValue("from", StringComparison.OrdinalIgnoreCase, out value))
                {
                    twilioConfig.From = value.ToString();
                }

                if (configSection.TryGetValue("to", StringComparison.OrdinalIgnoreCase, out value))
                {
                    twilioConfig.To = value.ToString();
                }

                if (configSection.TryGetValue("body", StringComparison.OrdinalIgnoreCase, out value))
                {
                    twilioConfig.Body = value.ToString();
                }
            }

            return twilioConfig;
        }

        /// <inheritdoc/>
        public override bool TryCreate(ScriptBindingContext context, out ScriptBinding binding)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            binding = null;

            if (string.Compare(context.Type, "twilioSMS", StringComparison.OrdinalIgnoreCase) == 0)
            {
                binding = new TwilioBinding(context);
            }

            return binding != null;
        }

        /// <inheritdoc/>
        public override void Initialize()
        {
            var twilioConfig = CreateConfiguration(Metadata);
            if (!string.IsNullOrEmpty(twilioConfig.AccountSid) && !string.IsNullOrEmpty(twilioConfig.AuthToken))
            {
                Config.UseTwilioSms(twilioConfig);
            }
        }

        /// <inheritdoc/>
        public override bool TryResolveAssembly(string assemblyName, out Assembly assembly)
        {
            assembly = null;

            if (string.Compare(assemblyName, _twilioAssemblyName, StringComparison.OrdinalIgnoreCase) == 0)
            {
                assembly = typeof(SMSMessage).Assembly;
            }

            return assembly != null;
        }

        private class TwilioBinding : ScriptBinding
        {
            public TwilioBinding(ScriptBindingContext context) : base(context)
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
                    new TwilioSmsAttribute
                    {
                        To = Context.GetMetadataValue<string>("to"),
                        From = Context.GetMetadataValue<string>("from"),
                        Body = Context.GetMetadataValue<string>("body")
                    }
                };
            }
        }
    }
}
