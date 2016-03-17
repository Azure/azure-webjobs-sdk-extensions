// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Azure.NotificationHubs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.NotificationHubs
{
    internal class Converter
    {
        public void AddConverters(ref IConverterManager converterManager)
        {
            converterManager.AddConverter<TemplateNotification, Notification>(templateNotification => templateNotification);
            converterManager.AddConverter<string, Notification>(messageProperties => BuildTemplateNotificationFromJson(messageProperties));
        }

        private TemplateNotification BuildTemplateNotificationFromJson(string messageProperties)
        {
            JObject message = JsonConvert.DeserializeObject<JObject>(messageProperties);
            Dictionary<string, string> templateProperties = new Dictionary<string, string>();
            foreach (JProperty property in message.Properties())
            {
                templateProperties.Add(property.Name.ToString(), property.Value.ToString());
            }
            return new TemplateNotification(templateProperties);
        }
    }
}
