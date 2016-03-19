// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Azure.NotificationHubs;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.NotificationHubs
{
    internal static class Converter
    {
        public static IConverterManager AddNotificationHubConverters(this IConverterManager converterManager)
        {
            converterManager.AddConverter<TemplateNotification, Notification>(templateNotification => templateNotification);
            converterManager.AddConverter<string, Notification>(messageProperties => BuildTemplateNotificationFromJsonString(messageProperties));
            converterManager.AddConverter<IDictionary<string, string>, Notification>(messageProperties => BuildTemplateNotificationFromDictionary(messageProperties));
            return converterManager;
        }

        internal static TemplateNotification BuildTemplateNotificationFromJsonString(string messageProperties)
        {
            JObject jobj = JObject.Parse(messageProperties);
            Dictionary<string, string> templateProperties = jobj.ToObject<Dictionary<string, string>>();
            return new TemplateNotification(templateProperties);
        }

        internal static TemplateNotification BuildTemplateNotificationFromDictionary(IDictionary<string, string> templateProperties)
        {
            return new TemplateNotification(templateProperties);
        }
    }
}
