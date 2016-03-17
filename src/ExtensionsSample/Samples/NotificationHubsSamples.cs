// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Azure.NotificationHubs;
using Microsoft.Azure.WebJobs;
using Newtonsoft.Json.Linq;

namespace ExtensionsSample
{
    // To use the NotificationHubsSample:
    // 1. Create a new Mobile App
    // 2. Create and configure NotificationHub
    // 3. Add the NotificationHubs connection string to a 'AzureWebJobsNotificationHubConnectionString' App Setting in app.config    
    // 4. Add the NotificationHubs Hub name to a 'AzureWebJobsNotificationHubName' App Setting in app.config    
    // 5. Use MobileApps client SDK to register template with NotificationHubs
    public static class NotificationHubsSamples
    {
        //NotificationHub binding out Notification type
        // The binding sends push notification to any clients registered with the template
        // method successfully exits.
        public static void SendNotification_Out_Notification(
            [TimerTrigger("*/15 * * * * *")] TimerInfo timerInfo,
            [NotificationHub] out Notification notification)
        {
            notification = GetTemplateNotification("Hello");
        }

        // NotificationHub binding out String
        // The binding sends push notification to any clients registered with the template
        // method successfully exits.
        public static void SendNotification_Out_String(
            [TimerTrigger("*/15 * * * * *")] TimerInfo timerInfo,
            [NotificationHub] out string messageProperties)
        {
            JObject message = new JObject();
            message["message"] = "Hello World";
            message["location"] = "Redmond";
            messageProperties = message.ToString();
        }

        // The binding sends multiple push notification to any clients registered with the template
        // method successfully exits.
        public static void SendNotificationsOnTimerTrigger(
            [TimerTrigger("*/30 * * * * *")] TimerInfo timerInfo,
            [NotificationHub] out Notification[] notifications)
        {
            notifications = new TemplateNotification[]
                {
                    GetTemplateNotification("Message1"),
                    GetTemplateNotification("Message2")
                };
        }

        //   The binding creates a strongly-typed AsyncCollector, which is used to send push notifications. 
        //   The binding does not do anything with the results when the function exits.  
        public static async void SendNotifications_AsyncCollector(
            [TimerTrigger("00:01")] TimerInfo timer,
            [NotificationHub] IAsyncCollector<Notification> notifications)
        {
            await notifications.AddAsync(GetTemplateNotification("Message1"));
            await notifications.AddAsync(GetTemplateNotification("Message2"));
        }
        private static TemplateNotification GetTemplateNotification(string message)
        {
            Dictionary<string, string> templateProperties = new Dictionary<string, string>();
            templateProperties["message"] = message;
            return new TemplateNotification(templateProperties);
        }
    }
}
