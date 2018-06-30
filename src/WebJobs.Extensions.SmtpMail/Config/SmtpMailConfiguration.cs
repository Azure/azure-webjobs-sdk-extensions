// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Mail;
using Client;
using Microsoft.Azure.WebJobs.Extensions.Bindings;
using Microsoft.Azure.WebJobs.Extensions.Config;
using Microsoft.Azure.WebJobs.Host.Config;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.SmtpMail
{
    /// <summary>
    /// Defines the configuration options for the SmtpMail binding.
    /// </summary>
    public class SmtpMailConfiguration : IExtensionConfigProvider
    {
        internal const string AzureWebJobsSmtpMailKeyName = "ConnectionStrings:AzureWebJobsSmtpMail";
        private ConcurrentDictionary<string, ISmtpMailClient> _smtpMailClientCache = new ConcurrentDictionary<string, ISmtpMailClient>(StringComparer.Ordinal);

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public SmtpMailConfiguration()
        {
            ClientFactory = new SmtpMailClientFactory();
        }

        internal ISmtpMailClientFactory ClientFactory { get; set; }

        /// <summary>
        /// Gets or sets the SmtpMail Host. If not explicitly set, the value will be defaulted
        /// to the value specified via the 'AzureWebJobsSmtpMail' app setting or the
        /// 'AzureWebJobsSmtpMail' environment variable.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Gets or sets the default "to" address that will be used for messages.
        /// This value can be overridden by job functions.
        /// </summary>
        /// <remarks>
        /// An example of when it would be useful to provide a default value for 'to' 
        /// would be for emailing your own admin account to notify you when particular
        /// jobs are executed. In this case, job functions can specify minimal info in
        /// their bindings, for example just a Subject and Text body.
        /// </remarks>
        public string ToAddress { get; set; }

        /// <summary>
        /// Gets or sets the default "from" address that will be used for messages.
        /// This value can be overridden by job functions.
        /// </summary>
        public string FromAddress { get; set; }

        /// <inheritdoc />
        public void Initialize(ExtensionConfigContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var metadata = new ConfigMetadata();
            context.ApplyConfig(metadata, "SmtpMail");
            ToAddress = SmtpMailHelpers.ApplyTo(ToAddress, metadata.To);
            FromAddress = SmtpMailHelpers.ApplyFrom(FromAddress, metadata.From);

            if (string.IsNullOrEmpty(ConnectionString))
            {
                INameResolver nameResolver = context.Config.NameResolver;
                ConnectionString = nameResolver.Resolve(AzureWebJobsSmtpMailKeyName);
            }

            var rule = context.AddBindingRule<SmtpMailAttribute>();
            rule.AddValidator(ValidateBinding);
            rule.BindToCollector(CreateCollector);
        }

        private IAsyncCollector<MailMessage> CreateCollector(SmtpMailAttribute attribute)
        {
            var connection = FirstOrDefault(attribute.Connection, ConnectionString);
            var smtpMail = _smtpMailClientCache.GetOrAdd(connection, conn => ClientFactory.Create(conn));

            return new SmtpMailMessageAsyncCollector(this, attribute, smtpMail);
        }

        private void ValidateBinding(SmtpMailAttribute attribute, Type type)
        {
            var connection = FirstOrDefault(attribute.Connection, ConnectionString);

            if (string.IsNullOrEmpty(connection))
            {
                throw new InvalidOperationException($"The SmtpMail connection must be set either via an '{AzureWebJobsSmtpMailKeyName}' connection string, via an '{AzureWebJobsSmtpMailKeyName}' environment variable, or directly in code via {nameof(SmtpMailConfiguration)}.{nameof(ConnectionString)} or {nameof(SmtpMailAttribute)}.{nameof(SmtpMailAttribute.Connection)}.");
            }
        }

        private static string FirstOrDefault(params string[] values)
        {
            return values.FirstOrDefault(item => !string.IsNullOrEmpty(item));
        }

        // Schema for host.json 
        private class ConfigMetadata
        {
            public string From { get; set; }

            public string To { get; set; }
        }
    }
}
