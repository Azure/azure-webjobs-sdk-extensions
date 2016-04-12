// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.NotificationHubs;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.NotificationHubs
{
    internal static class Converter
    {
        public static string NotificationPlatform { get; set; }

        public static IConverterManager AddNotificationHubConverters(this IConverterManager converterManager)
        {
            converterManager.AddConverter<TemplateNotification, Notification>(templateNotification => templateNotification);
            converterManager.AddConverter<string, Notification>(notificationAsString => BuildNotificationFromString(notificationAsString));
            converterManager.AddConverter<IDictionary<string, string>, Notification>(messageProperties => BuildTemplateNotificationFromDictionary(messageProperties));
            return converterManager;
        }

        internal static Notification BuildNotificationFromString(string notificationAsString)
        {
            Notification notification = null;
            if (string.IsNullOrEmpty(NotificationPlatform))
            {
                JObject jobj = JObject.Parse(notificationAsString);
                Dictionary<string, string> templateProperties = jobj.ToObject<Dictionary<string, string>>();
                notification = new TemplateNotification(templateProperties);
            }
            else
            {
                NotificationPlatform platform;
                try
                {
                    platform = (NotificationPlatform)Enum.Parse(typeof(NotificationPlatform), NotificationPlatform);
                }
                catch (ArgumentException)
                {
                    string validPlatforms = string.Empty;
                    foreach (NotificationPlatform notificationPlatform in Enum.GetValues(typeof(NotificationPlatform)))
                    {
                        validPlatforms += notificationPlatform.ToString() + "\n";
                    }
                    throw new ArgumentException("Invalid NotificationPlatform.Platforms supported: \n" + validPlatforms);
                }

                switch (platform)
                {
                    case Azure.NotificationHubs.NotificationPlatform.Wns:
                        notification = new WindowsNotification(notificationAsString);
                        break;
                    case Azure.NotificationHubs.NotificationPlatform.Apns:
                        notification = new AppleNotification(notificationAsString);
                        break;
                    case Azure.NotificationHubs.NotificationPlatform.Gcm:
                        notification = new GcmNotification(notificationAsString);
                        break;
                    case Azure.NotificationHubs.NotificationPlatform.Adm:
                        notification = new AdmNotification(notificationAsString);
                        break;
                    case Azure.NotificationHubs.NotificationPlatform.Mpns:
                        notification = new MpnsNotification(notificationAsString);
                        break;
                }
            }

            return notification;
        }

        internal static TemplateNotification BuildTemplateNotificationFromDictionary(IDictionary<string, string> templateProperties)
        {
            return new TemplateNotification(templateProperties);
        }
    }
}
