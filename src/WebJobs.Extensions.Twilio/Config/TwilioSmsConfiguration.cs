// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using Microsoft.Azure.WebJobs.Extensions.Bindings;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using Newtonsoft.Json.Linq;
using Twilio;

namespace Microsoft.Azure.WebJobs.Extensions.Twilio
{
    public class TwilioSmsConfiguration : IExtensionConfigProvider
    {
        internal const string AzureWebJobsTwilioAccountSidKeyName = "AzureWebJobsTwilioAccountSid";
        internal const string AzureWebJobsTwilioAccountAuthTokenName = "AzureWebJobsTwilioAuthToken";

        private readonly ConcurrentDictionary<Tuple<string, string>, TwilioRestClient> _twilioClientCache = new ConcurrentDictionary<Tuple<string, string>, TwilioRestClient>();
        
        private string _defaultAccountSid;
        private string _defaultAuthToken;

        public string AccountSid { get; set; }

        public string AuthToken { get; set; }

        public string Body { get; set; }

        public string From { get; set; }

        public string To { get; set; }

        public void Initialize(ExtensionConfigContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            INameResolver nameResolver = context.Config.GetService<INameResolver>();

            _defaultAccountSid = nameResolver.Resolve(AzureWebJobsTwilioAccountSidKeyName);
            _defaultAuthToken = nameResolver.Resolve(AzureWebJobsTwilioAccountAuthTokenName);

            IConverterManager converterManager = context.Config.GetService<IConverterManager>();
            converterManager.AddConverter<JObject, SMSMessage>(CreateSmsMessage);

            BindingFactory factory = new BindingFactory(nameResolver, converterManager);
            IBindingProvider outputProvider = factory.BindToAsyncCollector<TwilioSmsAttribute, SMSMessage>((attr) =>
            {
                return new TwilioSmsMessageAsyncCollector(CreateContext(attr));
            });

            IExtensionRegistry extensions = context.Config.GetService<IExtensionRegistry>();
            extensions.RegisterBindingRules<TwilioSmsAttribute>(ValidateBinding, nameResolver, outputProvider);
        }

        private void ValidateBinding(TwilioSmsAttribute attribute, Type type)
        {
            string accountSid = Utility.FirstOrDefault(attribute.AccountSidSetting, AccountSid, _defaultAccountSid);
            string authToken = Utility.FirstOrDefault(attribute.AuthTokenSetting, AuthToken, _defaultAuthToken);
            if (string.IsNullOrEmpty(accountSid))
            {
                ThrowMissingSettingException("AccountSID", AzureWebJobsTwilioAccountSidKeyName, "AccountSID");
            }

            if (string.IsNullOrEmpty(authToken))
            {
                ThrowMissingSettingException("AuthToken", AzureWebJobsTwilioAccountSidKeyName, "AuthToken");
            }
        }

        private TwilioSmsContext CreateContext(TwilioSmsAttribute attribute)
        {
            string accountSid = Utility.FirstOrDefault(attribute.AccountSidSetting, AccountSid, _defaultAccountSid);
            string authToken = Utility.FirstOrDefault(attribute.AuthTokenSetting, AuthToken, _defaultAuthToken);

            TwilioRestClient client = _twilioClientCache.GetOrAdd(new Tuple<string, string>(accountSid, authToken), t => new TwilioRestClient(t.Item1, t.Item2));

            var context = new TwilioSmsContext
            {
                Client = client,
                Body = Utility.FirstOrDefault(attribute.Body, Body),
                From = Utility.FirstOrDefault(attribute.From, From),
                To = Utility.FirstOrDefault(attribute.To, To)
            };

            return context;
        }

        private SMSMessage CreateSmsMessage(JObject messageJson)
        {
            var message = new SMSMessage();
            message.From = GetPropertyValue(messageJson, "from");
            message.To = GetPropertyValue(messageJson, "to");
            message.Body = GetPropertyValue(messageJson, "body");

            return message;
        }

        private static string GetPropertyValue(JObject messageObject, string propertyName)
        {
            JToken result = null;
            if (messageObject.TryGetValue(propertyName, StringComparison.OrdinalIgnoreCase, out result))
            {
                return result.ToString();
            }

            return null;
        }

        private static string ThrowMissingSettingException(string settingDisplayName, string settingName, string configPropertyName)
        {
            string message = string.Format("The Twilio {0} must be set either via a '{1}' app setting, via a '{1}' environment variable, or directly in code via TwilioSmsConfiguration.{2}.",
                settingDisplayName, settingName, configPropertyName);

            throw new InvalidOperationException(message);
        }
    }
}
