// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using Microsoft.Azure.WebJobs.Extensions.SmtpMail;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.Bindings
{
    internal static class SmtpMailHelpers
    {
        public static string ApplyFrom(string current, string value)
        {
            if (!string.IsNullOrEmpty(value) && TryParseAddress(value, out MailAddress mail))
            {
                return value;
            }

            return current;
        }

        public static string ApplyTo(string current, string value)
        {
            if (!string.IsNullOrEmpty(value) && TryParseAddress(value, out MailAddressCollection mail))
            {
                return value;
            }

            return current;
        }

        private static bool TryParseAddress(string value, out MailAddress email)
        {
            email = null;

            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            try
            {
                email = new MailAddress(value);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static bool TryParseAddress(string value, out MailAddressCollection emails)
        {
            emails = null;

            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            try
            {
                emails = new MailAddressCollection { value };
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        public static void DefaultMessageProperties(MailMessage mail, SmtpMailConfiguration config, SmtpMailAttribute attribute)
        {
            // Apply message defaulting for 'From' field.
            if (!IsFromValid(mail))
            {
                if (!string.IsNullOrEmpty(attribute.From))
                {
                    if (!TryParseAddress(attribute.From, out MailAddress from))
                    {
                        throw new ArgumentException($"Invalid '{nameof(mail.From)}' address specified", nameof(mail.From));
                    }
                    mail.From = from;
                }
                else if (config.FromAddress != null && TryParseAddress(config.FromAddress, out MailAddress from))
                {
                    mail.From = from;
                }
            }

            // Apply message defaulting for 'To' field.
            if (!IsToValid(mail))
            {
                if (!string.IsNullOrEmpty(attribute.To))
                {
                    if (!TryParseAddress(attribute.To, out MailAddressCollection toList))
                    {
                        throw new ArgumentException($"Invalid '{nameof(mail.To)}' address specified", nameof(mail.To));
                    }

                    mail.To.Clear();
                    foreach (var to in toList)
                    {
                        mail.To.Add(to);
                    }
                }
                else if (config.ToAddress != null && TryParseAddress(config.ToAddress, out MailAddressCollection tos))
                {
                    mail.To.Clear();
                    foreach (var to in tos)
                    {
                        mail.To.Add(to);
                    }
                }
            }

            // Apply message defaulting for 'Subject' field.
            if (string.IsNullOrEmpty(mail.Subject) && !string.IsNullOrEmpty(attribute.Subject))
            {
                mail.Subject = attribute.Subject;
                mail.SubjectEncoding = Encoding.UTF8;
            }

            // Apply message defaulting for 'Text' field.
            // TEXT should be added as the first view, considering RFC2056, https://tools.ietf.org/html/rfc2046#section-5.1.4
            if ((mail.Body == null || mail.Body.Length == 0) && !string.IsNullOrEmpty(attribute.Text))
            {
                mail.IsBodyHtml = false;
                mail.BodyEncoding = Encoding.UTF8;
                var view = AlternateView.CreateAlternateViewFromString(attribute.Text, Encoding.UTF8, MediaTypeNames.Text.Plain);
                mail.AlternateViews.Add(view);
            }

            // Apply message defaulting for 'Html' field.
            if ((mail.Body == null || mail.Body.Length == 0) && !string.IsNullOrEmpty(attribute.Html))
            {
                mail.IsBodyHtml = true;
                mail.BodyEncoding = Encoding.UTF8;
                var view = AlternateView.CreateAlternateViewFromString(attribute.Html, Encoding.UTF8, MediaTypeNames.Text.Html);
                mail.AlternateViews.Add(view);
            }
        }

        public static bool IsFromValid(MailMessage mail)
        {
            return mail.From != null && !string.IsNullOrEmpty(mail.From.Address);
        }

        public static bool IsToValid(MailMessage mail)
        {
            return mail.To.Count > 0 && mail.To.All(to => !string.IsNullOrEmpty(to.Address));
        }
    }
}
