// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net.Mail;
using Microsoft.Azure.WebJobs.Extensions.SendGrid;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.SendGrid
{
    public class SendGridAttributeBindingProviderTests
    {
        [Fact]
        public void ParseFromAddress_Success()
        {
            MailAddress address = null;
            SendGridBinding.ParseFromAddress("test@test.com", out address);
            Assert.Equal("test@test.com", address.Address);
            Assert.Equal(string.Empty, address.DisplayName);

            address = null;
            SendGridBinding.ParseFromAddress("test@test.com:Test Account", out address);
            Assert.Equal("test@test.com", address.Address);
            Assert.Equal("Test Account", address.DisplayName);

            address = null;
            SendGridBinding.ParseFromAddress("test@test.com:Test Acco:unt", out address);
            Assert.Equal("test@test.com", address.Address);
            Assert.Equal("Test Acco:unt", address.DisplayName);
        }

        [Theory]
        [InlineData("")]
        [InlineData("invalid")]
        public void ParseFromAddress_Failure(string value)
        {
            MailAddress address = null;
            bool result = SendGridBinding.ParseFromAddress(value, out address);
            Assert.False(result);
            Assert.Null(address);
        }
    }
}
