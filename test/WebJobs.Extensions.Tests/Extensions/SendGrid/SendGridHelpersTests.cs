// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Linq;
using System.Net.Mail;
using Microsoft.Azure.WebJobs.Extensions.Bindings;
using Microsoft.Azure.WebJobs.Extensions.SendGrid;
using Newtonsoft.Json.Linq;
using SendGrid;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.SendGrid
{
    public class SendGridHelpersTests
    {
        [Fact]
        public void TryParseAddress_Success()
        {
            MailAddress address = null;
            SendGridHelpers.TryParseAddress("test@test.com", out address);
            Assert.Equal("test@test.com", address.Address);
            Assert.Equal(string.Empty, address.DisplayName);

            address = null;
            SendGridHelpers.TryParseAddress("test@test.com:Test Account", out address);
            Assert.Equal("test@test.com", address.Address);
            Assert.Equal("Test Account", address.DisplayName);

            address = null;
            SendGridHelpers.TryParseAddress("test@test.com:Test Acco:unt", out address);
            Assert.Equal("test@test.com", address.Address);
            Assert.Equal("Test Acco:unt", address.DisplayName);
        }

        [Theory]
        [InlineData("")]
        [InlineData("invalid")]
        public void TryParseAddress_Failure(string value)
        {
            MailAddress address = null;
            bool result = SendGridHelpers.TryParseAddress(value, out address);
            Assert.False(result);
            Assert.Null(address);
        }

        [Fact]
        public void DefaultMessageProperties_CreatesExpectedMessage()
        {
            SendGridAttribute attribute = new SendGridAttribute();
            SendGridConfiguration config = new SendGridConfiguration
            {
                ApiKey = "12345",
                FromAddress = new MailAddress("test2@test.com", "Test2"),
                ToAddress = new MailAddress("test@test.com", "Test")
            };
            SendGridMessage message = new SendGridMessage
            {
                Subject = "TestSubject",
                Text = "TestText"
            };

            SendGridHelpers.DefaultMessageProperties(message, config, attribute);

            Assert.Same(config.FromAddress, config.FromAddress);
            Assert.Equal("test@test.com", message.To.Single().Address);
            Assert.Equal("TestSubject", message.Subject);
            Assert.Equal("TestText", message.Text);
        }

        [Fact]
        public void CreateMessage_CreatesExpectedMessage()
        {
            // multiple recipients
            JObject input = new JObject
            {
                { "to", new JArray {
                        "test1@acme.com",
                        "test2@acme.com:Test Account 2"
                    }
                },
                { "from", "test3@contoso.com:Test Account 3" },
                { "subject", "Test Subject" },
                { "text", "Test Text" }
            };

            SendGridMessage result = SendGridHelpers.CreateMessage(input);

            Assert.Equal(2, result.To.Length);
            Assert.Equal("test1@acme.com", result.To[0].Address);
            Assert.Equal("test2@acme.com", result.To[1].Address);
            Assert.Equal("Test Account 2", result.To[1].DisplayName);
            Assert.Equal("test3@contoso.com", result.From.Address);
            Assert.Equal("Test Account 3", result.From.DisplayName);
            Assert.Equal("Test Subject", result.Subject);
            Assert.Equal("Test Text", result.Text);

            // single recipient
            input = new JObject
            {
                { "to", "test2@acme.com:Test Account 2" },
                { "from", "test3@contoso.com:Test Account 3" },
                { "subject", "Test Subject" },
                { "text", "Test Text" }
            };

            result = SendGridHelpers.CreateMessage(input);

            Assert.Equal(1, result.To.Length);
            Assert.Equal("test2@acme.com", result.To[0].Address);
            Assert.Equal("Test Account 2", result.To[0].DisplayName);
            Assert.Equal("test3@contoso.com", result.From.Address);
            Assert.Equal("Test Account 3", result.From.DisplayName);
            Assert.Equal("Test Subject", result.Subject);
            Assert.Equal("Test Text", result.Text);
        }

        [Fact]
        public void CreateConfiguration_CreatesExpectedConfiguration()
        {
            JObject config = new JObject();
            var result = SendGridHelpers.CreateConfiguration(config);

            Assert.Null(result.FromAddress);
            Assert.Null(result.ToAddress);

            config = new JObject
            {
                { "sendGrid", new JObject
                    {
                        { "to", "test1@test.com:Testing1" },
                        { "from", "test2@test.com:Testing2" }
                    }
                }
            };
            result = SendGridHelpers.CreateConfiguration(config);

            Assert.Equal("test1@test.com", result.ToAddress.Address);
            Assert.Equal("Testing1", result.ToAddress.DisplayName);
            Assert.Equal("test2@test.com", result.FromAddress.Address);
            Assert.Equal("Testing2", result.FromAddress.DisplayName);
        }
    }
}
