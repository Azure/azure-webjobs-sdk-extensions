// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Bindings;
using Microsoft.Azure.WebJobs.Extensions.SendGrid;
using Microsoft.Extensions.Configuration;
using SendGrid.Helpers.Mail;
using Xunit;

namespace SendGridTests
{
    public class SendGridHelpersTests
    {
        [Fact]
        public void TryParseAddress_Success()
        {
            EmailAddress address = null;
            SendGridHelpers.TryParseAddress("test@test.com", out address);
            Assert.Equal("test@test.com", address.Email);
            Assert.Null(address.Name);

            address = null;
            SendGridHelpers.TryParseAddress("Test Account <test@test.com>", out address);
            Assert.Equal("test@test.com", address.Email);
            Assert.Equal("Test Account", address.Name);
        }

        [Theory]
        [InlineData("")]
        [InlineData("invalid")]
        public void TryParseAddress_Failure(string value)
        {
            EmailAddress address = null;
            bool result = SendGridHelpers.TryParseAddress(value, out address);
            Assert.False(result);
            Assert.Null(address);
        }

        [Fact]
        public void DefaultMessageProperties_CreatesExpectedMessage()
        {
            SendGridAttribute attribute = new SendGridAttribute();
            SendGridOptions options = new SendGridOptions
            {
                ApiKey = "12345",
                FromAddress = new EmailAddress("test2@test.com", "Test2"),
                ToAddress = new EmailAddress("test@test.com", "Test")
            };

            SendGridMessage message = new SendGridMessage();
            message.Subject = "TestSubject";
            message.AddContent("text/plain", "TestText");

            SendGridHelpers.DefaultMessageProperties(message, options, attribute);

            Assert.Same(options.FromAddress, options.FromAddress);
            Assert.Equal("test@test.com", message.Personalizations.Single().Tos.Single().Email);
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

            SendGridMessage result = SendGridHelpers.CreateMessage(mail);

            Personalization p = result.Personalizations.Single();
            Assert.Equal(2, p.Tos.Count);
            Assert.Equal("test1@acme.com", p.Tos[0].Email);
            Assert.Equal("test2@acme.com", p.Tos[1].Email);
            Assert.Equal("Test Account 2", p.Tos[1].Name);
            Assert.Equal("test3@contoso.com", result.From.Email);
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
            p = result.Personalizations.Single();

            Assert.Equal(1, p.Tos.Count);
            Assert.Equal("test2@acme.com", p.Tos[0].Email);
            Assert.Equal("Test Account 2", p.Tos[0].Name);
            Assert.Equal("test3@contoso.com", result.From.Email);
            Assert.Equal("Test Account 3", result.From.Name);
            Assert.Equal("Test Subject", result.Subject);
            Assert.Equal("Test Text", result.Contents.Single().Value);
        }

        [Fact]
        public void ApplyConfigurationSection_CreatesExpectedOptions()
        {
            var options = new SendGridOptions();
            var dict = new Dictionary<string, string>();
            var builder = new ConfigurationBuilder();
            builder.AddInMemoryCollection(dict);
            var config = builder.Build();
            SendGridHelpers.ApplyConfiguration(config, options);
            Assert.Null(options.FromAddress);
            Assert.Null(options.ToAddress);

            dict = new Dictionary<string, string>
            {
                { "to", "Testing1 <test1@test.com>" },
                { "from", "Testing2 <test2@test.com>" },
            };
            builder = new ConfigurationBuilder();
            builder.AddInMemoryCollection(dict);
            config = builder.Build();

            SendGridHelpers.ApplyConfiguration(config, options);

            Assert.Equal("test1@test.com", options.ToAddress.Email);
            Assert.Equal("Testing1", options.ToAddress.Name);
            Assert.Equal("test2@test.com", options.FromAddress.Email);
            Assert.Equal("Testing2", options.FromAddress.Name);
        }

        [Fact]
        public void IsToValid()
        {
            // Null Personalization
            SendGridMessage mail = new SendGridMessage();
            mail.Personalizations = null;
            Assert.False(SendGridHelpers.IsToValid(mail));

            // Empty Personalization
            mail.Personalizations = new List<Personalization>();
            Assert.False(SendGridHelpers.IsToValid(mail));

            // 'To' with no address
            Personalization personalization = new Personalization();
            personalization.Tos = new List<EmailAddress>
            {
                new EmailAddress()
            };
            mail.Personalizations.Add(personalization);
            Assert.False(SendGridHelpers.IsToValid(mail));

            // Personalization with no 'To'
            mail = new SendGridMessage();
            mail.Personalizations = new List<Personalization>();

            Personalization personalization1 = new Personalization();
            personalization1.Tos = new List<EmailAddress>
            {
                new EmailAddress("test1@test.com")
            };
            mail.Personalizations.Add(personalization1);

            Personalization personalization2 = new Personalization();
            personalization2.Bccs = new List<EmailAddress>
            {
                new EmailAddress("test2@test.com")
            };
            mail.Personalizations.Add(personalization2);

            Assert.False(SendGridHelpers.IsToValid(mail));

            // valid
            personalization2.Tos = new List<EmailAddress>
            {
                new EmailAddress("test3@test.com")
            };
            Assert.True(SendGridHelpers.IsToValid(mail));
        }
    }
}
