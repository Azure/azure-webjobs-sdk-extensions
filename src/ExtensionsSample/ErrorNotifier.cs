// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mail;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions;
using Microsoft.Azure.WebJobs.Extensions.SendGrid;
using SendGrid;

namespace ExtensionsSample
{
    /// <summary>
    /// Contains some sample notification methods used as subscriptions
    /// for <see cref="TraceMonitor"/> events.
    /// </summary>
    public class ErrorNotifier
    {
        private const string NotificationUriSettingName = "AzureWebJobsErrorNotificationUri";
        private readonly string _webNotificationUri;
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly Web _sendGrid;
        private SendGridConfiguration _sendGridConfig;

        public ErrorNotifier(SendGridConfiguration sendGridConfig)
        {
            if (sendGridConfig == null)
            {
                throw new ArgumentNullException("sendGridConfig");
            }
            _sendGridConfig = sendGridConfig;
            _sendGrid = new Web(sendGridConfig.ApiKey);

            // pull our IFTTT notification URL from app settings (since it contains a secret key)
            var nameResolver = new DefaultNameResolver();
            _webNotificationUri = nameResolver.Resolve(NotificationUriSettingName);
        }

        /// <summary>
        /// Send a WebHook request to an IFTTT WebHook event that can be configured
        /// to send an SMS message, etc.
        /// </summary>
        /// <remarks>
        /// Using IFTTT is free, and by sending a WebHook request on an event,
        /// you can use any of the many other notification mechanisms they support
        /// (email, SMS, etc.) See IFTTT Maker Channel documentation here: http://ifttt.com/maker.
        /// </remarks>
        /// <param name="filter">The <see cref="TraceFilter"/> that triggered the notification.</param>
        public void WebNotify(TraceFilter filter)
        {
            if (string.IsNullOrEmpty(_webNotificationUri))
            {
                return;
            }

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, _webNotificationUri);

            // Send some event detail data along to the IFTTT recipe.
            string json = string.Format("{{ \"value1\": \"{0}\" }}", filter.Message);
            request.Content = new StringContent(json);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            _httpClient.SendAsync(request).Wait();
        }

        /// <summary>
        /// Send an email notification using SendGrid.
        /// </summary>
        /// <param name="filter">The <see cref="TraceFilter"/> that triggered the notification.</param>
        public void EmailNotify(TraceFilter filter)
        {
            SendGridMessage message = new SendGridMessage()
            {
                From = _sendGridConfig.FromAddress,
                Subject = "WebJob Error Notification",
                Text = filter.GetDetailedMessage(5)
            };
            message.To = new MailAddress[] { _sendGridConfig.ToAddress };

            _sendGrid.DeliverAsync(message);
        }
    }
}
