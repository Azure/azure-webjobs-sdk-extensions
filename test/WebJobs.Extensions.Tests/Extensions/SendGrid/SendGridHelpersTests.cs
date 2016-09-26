// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.WebJobs.Extensions.Bindings;
using Microsoft.Azure.WebJobs.Extensions.SendGrid;
using Newtonsoft.Json.Linq;
using SendGrid.Helpers.Mail;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.SendGrid
{
    public class SendGridHelpersTests
    {
        [Fact]
        public void TryParseAddress_Success()
        {
            Email address = null;
            SendGridHelpers.TryParseAddress("test@test.com", out address);
            Assert.Equal("test@test.com", address.Address);
            Assert.Null(address.Name);

            address = null;
            SendGridHelpers.TryParseAddress("Test Account <test@test.com>", out address);
            Assert.Equal("test@test.com", address.Address);
            Assert.Equal("Test Account", address.Name);
        }

        [Theory]
        [InlineData("")]
        [InlineData("invalid")]
        public void TryParseAddress_Failure(string value)
        {
            Email address = null;
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
                FromAddress = new Email("test2@test.com", "Test2"),
                ToAddress = new Email("test@test.com", "Test")
            };

            Mail message = new Mail();
            message.Subject = "TestSubject";
            message.AddContent(new Content("text/plain", "TestText"));

            SendGridHelpers.DefaultMessageProperties(message, config, attribute);

            Assert.Same(config.FromAddress, config.FromAddress);
            Assert.Equal("test@test.com", message.Personalization.Single().Tos.Single().Address);
            Assert.Equal("TestSubject", message.Subject);
            Assert.Equal("TestText", message.Contents.Single().Value);
        }

        [Fact]
        public void CreateMessage_CreatesExpectedMessage()
        {
            // multiple recipients
            string mail = @"{
              'personalizations': [
                {
                  'to': [
                    {
                      'email': 'test1@acme.com'
                    },
                    {
                      'email': 'test2@acme.com',
                      'name': 'Test Account 2'
                    }
                  ]
                }
              ],
              'from': {
                'email': 'test3@contoso.com',
                'name': 'Test Account 3'
              },
              'subject': 'Test Subject',
              'content': [
                {
                  'type': 'text/plain',
                  'value': 'Test Text'
                }
              ]
            }";

            Mail result = SendGridHelpers.CreateMessage(mail);

            Personalization p = result.Personalization.Single();
            Assert.Equal(2, p.Tos.Count);
            Assert.Equal("test1@acme.com", p.Tos[0].Address);
            Assert.Equal("test2@acme.com", p.Tos[1].Address);
            Assert.Equal("Test Account 2", p.Tos[1].Name);
            Assert.Equal("test3@contoso.com", result.From.Address);
            Assert.Equal("Test Account 3", result.From.Name);
            Assert.Equal("Test Subject", result.Subject);
            Assert.Equal("Test Text", result.Contents.Single().Value);

            // single recipient
            mail = @"{
              'personalizations': [
                {
                  'to': [
                    {
                      'email': 'test2@acme.com',
                      'name': 'Test Account 2'
                    }
                  ]
                }
              ],
              'from': {
                'email': 'test3@contoso.com',
                'name': 'Test Account 3'
              },
              'subject': 'Test Subject',
              'content': [
                {
                  'type': 'text/plain',
                  'value': 'Test Text'
                }
              ]
            }";

            result = SendGridHelpers.CreateMessage(mail);
            p = result.Personalization.Single();

            Assert.Equal(1, p.Tos.Count);
            Assert.Equal("test2@acme.com", p.Tos[0].Address);
            Assert.Equal("Test Account 2", p.Tos[0].Name);
            Assert.Equal("test3@contoso.com", result.From.Address);
            Assert.Equal("Test Account 3", result.From.Name);
            Assert.Equal("Test Subject", result.Subject);
            Assert.Equal("Test Text", result.Contents.Single().Value);
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
                        { "to", "Testing1 <test1@test.com>" },
                        { "from", "Testing2 <test2@test.com>" }
                    }
                }
            };
            result = SendGridHelpers.CreateConfiguration(config);

            Assert.Equal("test1@test.com", result.ToAddress.Address);
            Assert.Equal("Testing1", result.ToAddress.Name);
            Assert.Equal("test2@test.com", result.FromAddress.Address);
            Assert.Equal("Testing2", result.FromAddress.Name);
        }

        [Fact]
        public void IsToValid()
        {
            // Null Personalization
            Mail mail = new Mail();
            mail.Personalization = null;
            Assert.False(SendGridHelpers.IsToValid(mail));

            // Empty Personalization
            mail.Personalization = new List<Personalization>();
            Assert.False(SendGridHelpers.IsToValid(mail));

            // 'To' with no address
            Personalization personalization = new Personalization();
            personalization.AddTo(new Email());
            mail.AddPersonalization(personalization);
            Assert.False(SendGridHelpers.IsToValid(mail));

            // Personalization with no 'To'
            mail = new Mail();

            Personalization personalization1 = new Personalization();
            personalization1.AddTo(new Email("test1@test.com"));

            Personalization personalization2 = new Personalization();
            personalization2.AddBcc(new Email("test2@test.com"));

            mail.AddPersonalization(personalization1);
            mail.AddPersonalization(personalization2);

            Assert.False(SendGridHelpers.IsToValid(mail));

            // valid
            personalization2.AddTo(new Email("test3@test.com"));
            Assert.True(SendGridHelpers.IsToValid(mail));
        }
    }
}
