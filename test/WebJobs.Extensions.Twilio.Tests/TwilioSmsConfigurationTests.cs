// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Extensions.Twilio;
using Newtonsoft.Json.Linq;
using Twilio.Rest.Api.V2010.Account;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.Twilio
{
    public class TwilioSmsConfigurationTests
    {
        [Fact]
        public static void CreateMessageOptions_ReturnsExpectedResult_Simple()
        {
            JObject options = new JObject
            {
                { "to", "+12223334444" },
                { "from", "+12223334444" },
                { "body", "Knock knock." }
            };

            var result = TwilioExtensionConfigProvider.CreateMessageOptions(options);
            Assert.Equal(options["to"], result.To.ToString());
            Assert.Equal(options["from"], result.From.ToString());
            Assert.Equal(options["body"], result.Body.ToString());
        }

        [Fact]
        public static void CreateMessageOptions_ReturnsExpectedResult_Full()
        {
            JObject options = new JObject
            {
                { "to", "+14254570421" },
                { "from", "+14254570422" },
                { "body", "Knock knock." },
                { "forceDelivery", true },
                { "validityPeriod", 123 },
                { "provideFeedback", true },
                { "maxPrice", 0.55 },
                { "applicationSid", "aaaa" },
                { "statusCallback", "http://aaa" },
                { "messagingServiceSid", "bbbb" },
                { "pathAccountSid", "ccc" },
                { "mediaUrl", new JArray { "http://aaa", "http://bbb" } },
                { "attempt", 1 },
                { "smartEncoded", true },
                { "shortenUrls", true },
                { "sendAt", DateTime.UtcNow.AddHours(1) },
                { "sendAsMms", true },
                { "contentSid", "ddd" },
                { "contentVariables", "eee" },
                { "contentRetention", MessageResource.ContentRetentionEnum.Retain.ToString() },
                { "addressRetention", MessageResource.AddressRetentionEnum.Retain.ToString() },
                { "persistentAction", new JArray { "aaa", "bbb" } },
                { "scheduleType", MessageResource.ScheduleTypeEnum.Fixed.ToString() },
            };

            var result = TwilioExtensionConfigProvider.CreateMessageOptions(options);
            Assert.Equal(options["to"], result.To.ToString());
            Assert.Equal(options["from"], result.From.ToString());
            Assert.Equal(options["body"], result.Body.ToString());
            Assert.Equal(options["forceDelivery"], result.ForceDelivery);
            Assert.Equal(options["validityPeriod"], result.ValidityPeriod);
            Assert.Equal(options["provideFeedback"], result.ProvideFeedback);
            Assert.Equal(options["maxPrice"], result.MaxPrice);
            Assert.Equal(options["applicationSid"], result.ApplicationSid);
            Assert.Equal(new Uri((string)options["statusCallback"]), result.StatusCallback);
            Assert.Equal(options["messagingServiceSid"], result.MessagingServiceSid);
            Assert.Equal(options["pathAccountSid"], result.PathAccountSid);
            Assert.Equal(new Uri((string)options["mediaUrl"][0]), result.MediaUrl[0]);
            Assert.Equal(new Uri((string)options["mediaUrl"][1]), result.MediaUrl[1]);
            Assert.Equal(options["attempt"], result.Attempt);
            Assert.Equal(options["smartEncoded"], result.SmartEncoded);
            Assert.Equal(options["shortenUrls"], result.ShortenUrls);
            Assert.Equal(options["sendAt"], result.SendAt);
            Assert.Equal(options["sendAsMms"], result.SendAsMms);
            Assert.Equal(options["contentSid"], result.ContentSid);
            Assert.Equal(options["contentVariables"], result.ContentVariables);
            Assert.Equal(options["contentRetention"], result.ContentRetention.ToString());
            Assert.Equal(options["addressRetention"], result.AddressRetention.ToString());
            Assert.Equal(options["persistentAction"][0], result.PersistentAction[0]);
            Assert.Equal(options["persistentAction"][1], result.PersistentAction[1]);
            Assert.Equal(options["scheduleType"], result.ScheduleType.ToString());
        }
    }
}
