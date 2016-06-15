// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Mail;
using Microsoft.Azure.WebJobs.Extensions.SendGrid;
using Newtonsoft.Json.Linq;
using SendGrid;

namespace Microsoft.Azure.WebJobs.Extensions.Bindings
{
    internal class SendGridHelpers
    {
        internal static bool TryParseAddress(string value, out MailAddress mailAddress)
        {
            mailAddress = null;

            int idx = value.IndexOf('@');
            if (idx < 0)
            {
                return false;
            }

            idx = value.IndexOf(':', idx);
            if (idx > 0)
            {
                string address = value.Substring(0, idx);
                string displayName = value.Substring(idx + 1);
                mailAddress = new MailAddress(address, displayName);
                return true;
            }
            else
            {
                mailAddress = new MailAddress(value);
                return true;
            }
        }

        internal static void DefaultMessageProperties(SendGridMessage message, SendGridConfiguration config, SendGridAttribute attribute)
        {
            // Apply message defaulting
            if (message.From == null)
            {
                if (!string.IsNullOrEmpty(attribute.From))
                {
                    MailAddress from = null;
                    if (!TryParseAddress(attribute.From, out from))
                    {
                        throw new ArgumentException("Invalid 'From' address specified");
                    }
                    message.From = from;
                }
                else if (config.FromAddress != null)
                {
                    message.From = config.FromAddress;
                }
            }

            if (message.To == null || message.To.Length == 0)
            {
                if (!string.IsNullOrEmpty(attribute.To))
                {
                    message.AddTo(attribute.To);
                }
                else if (config.ToAddress != null)
                {
                    message.To = new MailAddress[] { config.ToAddress };
                }
            }

            if (string.IsNullOrEmpty(message.Subject) &&
                !string.IsNullOrEmpty(attribute.Subject))
            {
                message.Subject = attribute.Subject;
            }

            if (string.IsNullOrEmpty(message.Text) &&
                !string.IsNullOrEmpty(attribute.Text))
            {
                message.Text = attribute.Text;
            }
        }

        internal static SendGridMessage CreateMessage(JObject input)
        {
            SendGridMessage message = new SendGridMessage();

            JToken value = null;
            MailAddress mailAddress = null;
            if (input.TryGetValue("to", StringComparison.OrdinalIgnoreCase, out value))
            {
                Collection<MailAddress> addresses = new Collection<MailAddress>();
                if (value.Type == JTokenType.Array)
                {
                    foreach (string address in value)
                    {
                        MailAddress to = null;
                        if (!TryParseAddress(address, out to))
                        {
                            throw new ArgumentException("Invalid 'To' address specified");
                        }
                        addresses.Add(to);
                    }
                }
                else if (value.Type == JTokenType.String)
                {
                    if (!TryParseAddress((string)value, out mailAddress))
                    {
                        throw new ArgumentException("Invalid 'To' address specified");
                    }
                    addresses.Add(mailAddress);
                }

                message.To = addresses.ToArray();
            }

            if (input.TryGetValue("from", StringComparison.OrdinalIgnoreCase, out value))
            {
                MailAddress from = null;
                if (!TryParseAddress((string)value, out from))
                {
                    throw new ArgumentException("Invalid 'From' address specified");
                }
                message.From = from;
            }

            if (input.TryGetValue("subject", StringComparison.OrdinalIgnoreCase, out value))
            {
                message.Subject = (string)value;
            }

            if (input.TryGetValue("text", StringComparison.OrdinalIgnoreCase, out value))
            {
                message.Text = (string)value;
            }

            return message;
        }
    }
}
