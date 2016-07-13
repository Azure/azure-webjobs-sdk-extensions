// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Extensions.Bindings;
using Microsoft.Azure.WebJobs.Extensions.Twilio;
using Twilio;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.Twilio
{
    public class TwilioSmsMessageAsyncCollectorTests
    {
        [Fact]
        public void ApplyDefaultSettings_WithSettingsApplied_PreservesOriginalSettings()
        {
            SMSMessage message = GetTestMessage();
            TwilioSmsContext config = GetTestContext();

            TwilioSmsMessageAsyncCollector.ApplyContextMessageSettings(message, config);

            Assert.Equal("ToMessage", message.To);
            Assert.Equal("FromMessage", message.From);
            Assert.Equal("BodyMessage", message.Body);
        }

        [Fact]
        public void ApplyDefaultSettings_WithoutMessageSettingsApplied_AppliesConfigSettings()
        {
            SMSMessage message = new SMSMessage();
            TwilioSmsContext config = GetTestContext();

            TwilioSmsMessageAsyncCollector.ApplyContextMessageSettings(message, config);

            Assert.Equal("ToConfig", message.To);
            Assert.Equal("FromConfig", message.From);
            Assert.Equal("BodyConfig", message.Body);
        }

        private SMSMessage GetTestMessage()
        {
            return new SMSMessage
            {
                To = "ToMessage",
                From = "FromMessage",
                Body = "BodyMessage"
            };
        }

        private TwilioSmsContext GetTestContext()
        {
            return new TwilioSmsContext
            {
                To = "ToConfig",
                From = "FromConfig",
                Body = "BodyConfig"
            };
        }
    }
}
