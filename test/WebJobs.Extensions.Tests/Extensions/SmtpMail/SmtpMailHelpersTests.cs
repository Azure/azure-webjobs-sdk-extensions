// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Linq;
using System.Net.Mail;
using Microsoft.Azure.WebJobs.Extensions.Bindings;
using Microsoft.Azure.WebJobs.Extensions.SmtpMail;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.SmtpMail
{
    public class SmtpMailHelpersTests
    {
        [Theory]
        [InlineData("test@contoso.com")]
        [InlineData("Test Account <test@test.com>")]
        public void TryApplyAddress_Success(string value)
        {
            var result = SmtpMailHelpers.ApplyTo(null, value);
            Assert.Equal(value, result);
        }

        [Theory]
        [InlineData("")]
        [InlineData("invalid")]
        [InlineData("test1@contoso.com;test2@contoso.com")]
        public void TryParseAddress_Failure(string value)
        {
            var result = SmtpMailHelpers.ApplyTo(null, value);
            Assert.Null(result);
        }

        [Fact]
        public void DefaultMessageProperties_CreatesExpectedMessage()
        {
            var attribute = new SmtpMailAttribute();
            var config = new SmtpMailConfiguration
            {
                ConnectionString = "Host=test.contoso.com;Port=1234",
                FromAddress = "test2@test.com",
                ToAddress = "test@test.com"
            };

            var message = new MailMessage
            {
                Subject = "TestSubject"
            };

            SmtpMailHelpers.DefaultMessageProperties(message, config, attribute);

            Assert.Same(config.FromAddress, config.FromAddress);
            Assert.Equal(config.ToAddress, message.To.Single().Address);
            Assert.Equal("TestSubject", message.Subject);
        }
    }
}
