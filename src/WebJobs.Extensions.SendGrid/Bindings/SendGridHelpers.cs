// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net.Mail;
using Microsoft.Azure.WebJobs.Extensions.SendGrid;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SendGrid.Helpers.Mail;

namespace Microsoft.Azure.WebJobs.Extensions.Bindings
{
    internal class SendGridHelpers
    {
        internal static EmailAddress Apply(EmailAddress current, string value)
        {
            EmailAddress mail;
            if (TryParseAddress(value, out mail))
            {
                return mail;
            }
            return current;
        }

        internal static bool TryParseAddress(string value, out EmailAddress email)
        {
            email = null;

            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            try
            {
                // MailAddress will auto-parse the name from a string like "testuser@test.com <Test User>"
                MailAddress mailAddress = new MailAddress(value);
                string displayName = string.IsNullOrEmpty(mailAddress.DisplayName) ? null : mailAddress.DisplayName;
                email = new EmailAddress(mailAddress.Address, displayName);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        internal static void DefaultMessageProperties(SendGridMessage mail, SendGridConfiguration config, SendGridAttribute attribute)
        {
            // Apply message defaulting
            if (mail.From == null)
            {
                if (!string.IsNullOrEmpty(attribute.From))
                {
                    EmailAddress from = null;
                    if (!TryParseAddress(attribute.From, out from))
                    {
                        throw new ArgumentException("Invalid 'From' address specified");
                    }
                    mail.From = from;
                }
                else if (config.FromAddress != null)
                {
                    mail.From = config.FromAddress;
                }
            }

            if (!IsToValid(mail))
            {
                if (!string.IsNullOrEmpty(attribute.To))
                {
                    EmailAddress to = null;
                    if (!TryParseAddress(attribute.To, out to))
                    {
                        throw new ArgumentException("Invalid 'To' address specified");
                    }

                    mail.AddTo(to);
                }
                else if (config.ToAddress != null)
                {
                    mail.AddTo(config.ToAddress);
                }
            }

            if (string.IsNullOrEmpty(mail.Subject) &&
                !string.IsNullOrEmpty(attribute.Subject))
            {
                mail.Subject = attribute.Subject;
            }

            if ((mail.Contents == null || mail.Contents.Count == 0) &&
                !string.IsNullOrEmpty(attribute.Text))
            {
                mail.AddContent("text/plain", attribute.Text);
            }
        }

        internal static SendGridMessage CreateMessage(string input)
        {
            JObject json = JObject.Parse(input);
            return CreateMessage(json);
        }

        internal static SendGridMessage CreateMessage(JObject input)
        {
            return input.ToObject<SendGridMessage>();
        }

        internal static string CreateString(SendGridMessage input)
        {
            return CreateString(JObject.FromObject(input));
        }

        internal static string CreateString(JObject input)
        {
            return input.ToString(Formatting.None);
        }

        internal static bool IsToValid(SendGridMessage item)
        {
            return item.Personalizations != null &&
                item.Personalizations.Count > 0 &&
                item.Personalizations.All(p => p.Tos != null && !p.Tos.Any(t => string.IsNullOrEmpty(t.Email)));
        }

        internal static SendGridConfiguration CreateConfiguration(JObject metadata)
        {
            SendGridConfiguration sendGridConfig = new SendGridConfiguration();

            JObject configSection = (JObject)metadata.GetValue("sendGrid", StringComparison.OrdinalIgnoreCase);
            JToken value = null;
            if (configSection != null)
            {
                EmailAddress mailAddress = null;
                if (configSection.TryGetValue("from", StringComparison.OrdinalIgnoreCase, out value) &&
                    TryParseAddress((string)value, out mailAddress))
                {
                    sendGridConfig.FromAddress = mailAddress;
                }

                if (configSection.TryGetValue("to", StringComparison.OrdinalIgnoreCase, out value) &&
                    TryParseAddress((string)value, out mailAddress))
                {
                    sendGridConfig.ToAddress = mailAddress;
                }
            }

            return sendGridConfig;
        }
    }
}
