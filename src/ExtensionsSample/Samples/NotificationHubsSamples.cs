// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Azure.NotificationHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.NotificationHubs;

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
        // The binding sends push notification to any clients registered with the template
        //   method successfully exits.
        public static void SendNotificationOnTimerTrigger(
            [TimerTrigger("*/15 * * * * *")] TimerInfo timerInfo,
            [NotificationHubs] out Notification notification)
        {
            notification = GetTemplateNotification("bar");
        }

        // The binding sends multiple push notification to any clients registered with the template
        //   method successfully exits.
        public static void SendNotificationsOnTimerTrigger(
            [TimerTrigger("*/30 * * * * *")] TimerInfo timerInfo,
            [NotificationHubs] out Notification[] notifications)
        {
            notifications = new TemplateNotification[]
                {
                    GetTemplateNotification("bar1"),
                    GetTemplateNotification("bar2")
                };
        }

        //   The binding creates a strongly-typed AsyncCollector, which is used to send push notifications. 
        //   The binding does not do anything with the results when the function exits.  
        public static async void SendNotifications_AsyncCollector(
            [TimerTrigger("00:01")] TimerInfo timer,
            [NotificationHubs] IAsyncCollector<Notification> asyncCollector)
        {
            await asyncCollector.AddAsync(GetTemplateNotification("foo1"));
            await asyncCollector.AddAsync(GetTemplateNotification("foo2"));
        }
        private static TemplateNotification GetTemplateNotification(string message)
        {
            Dictionary<string, string> templateProperties = new Dictionary<string, string>();
            templateProperties["message"] = message;
            return new TemplateNotification(templateProperties);
        }
    }
}
