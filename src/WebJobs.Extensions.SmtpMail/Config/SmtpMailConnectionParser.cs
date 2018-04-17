// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Mail;

namespace Config
{
    internal static class SmtpMailConnectionParser
    {
        public static SmtpClient Parse(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentNullException(nameof(connectionString));
            }

            var settings = ParseStringIntoSettings(connectionString, error => throw new FormatException(error));
            if (settings.ReadString("Host", out var host))
            {
                var smtpClient = new SmtpClient(host);
                if (settings.ReadInt("Port", out var port))
                {
                    smtpClient.Port = port;
                }
                if (settings.ReadBool("EnableSsl", out var enableSsl))
                {
                    smtpClient.EnableSsl = enableSsl;
                }
                if (settings.ReadEnum<SmtpDeliveryFormat>("DeliveryFormat", out var deliveryFormat))
                {
                    smtpClient.DeliveryFormat = deliveryFormat;
                }
                if (settings.ReadInt("Timeout", out var timeout))
                {
                    smtpClient.Timeout = timeout;
                }
                if (settings.ReadString("TargetName", out var targetName))
                {
                    smtpClient.TargetName = targetName;
                }
                if (settings.ReadBool("UseDefaultCredentials", out var useDefaultCredentials))
                {
                    smtpClient.UseDefaultCredentials = useDefaultCredentials;
                }
                if (settings.ReadString("Username", out var username))
                {
                    settings.ReadString("Password", out var password);
                    smtpClient.Credentials = new NetworkCredential(username, password);
                }

                return smtpClient;
            }

            throw new FormatException("No valid combination of SmtpMail information found. Please at least specify the SMTP 'Host'.");
        }

        private static bool ReadString(this IDictionary<string, string> settings, string key, out string value)
        {
            return settings.TryGetValue(key, out value);
        }

        private static bool ReadInt(this IDictionary<string, string> settings, string key, out int value)
        {
            value = default;
            return settings.TryGetValue(key, out string textValue) && int.TryParse(textValue, out value);
        }

        private static bool ReadBool(this IDictionary<string, string> settings, string key, out bool value)
        {
            value = default;
            return settings.TryGetValue(key, out string textValue) && bool.TryParse(textValue, out value);
        }

        private static bool ReadEnum<TEnum>(this IDictionary<string, string> settings, string key, out TEnum value)
            where TEnum : struct
        {
            value = default;
            return settings.TryGetValue(key, out var valueText) && Enum.TryParse(valueText, true, out value);
        }

        private static IDictionary<string, string> ParseStringIntoSettings(string connectionString, Action<string> error)
        {
            var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var splitted = connectionString.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var nameValue in splitted)
            {
                var splittedNameValue = nameValue.Split(new[] { '=' }, 2);

                if (splittedNameValue.Length != 2)
                {
                    error("Settings must be of the form \"name=value\".");
                    return null;
                }

                var name = splittedNameValue[0];
                if (settings.ContainsKey(name))
                {
                    error(string.Format(CultureInfo.InvariantCulture, "Duplicate setting '{0}' found.", splittedNameValue[0]));
                    return null;
                }

                var value = splittedNameValue[1];
                settings.Add(name, value);
            }

            return settings;
        }
    }
}
