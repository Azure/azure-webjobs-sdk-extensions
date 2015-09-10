// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using SendGrid;
using Xunit;

using SendGridValueBinder = Microsoft.Azure.WebJobs.Extensions.SendGridAttributeBindingProvider.SendGridBinding.SendGridValueBinder;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.SendGrid
{
    public class SendGridValueBinderTests
    {
        private readonly SendGridValueBinder _valueBinder;
        private readonly SendGridMessage _message;

        public SendGridValueBinderTests()
        {
            Web web = new Web("1234");
            _message = new SendGridMessage();
            _valueBinder = new SendGridValueBinder(web, _message);
        }

        [Fact]
        public void Type_ReturnsExpectedType()
        {
            Assert.Equal(typeof(SendGridMessage), _valueBinder.Type);
        }

        [Fact]
        public void GetValue_ReturnsExpectedValue()
        {
            Assert.Same(_message, _valueBinder.GetValue());
        }

        [Fact]
        public void ToInvokeString_ReturnsNull()
        {
            Assert.Null(_valueBinder.ToInvokeString());
        }

        [Fact]
        public async Task SetValueAsync_SendsMessage()
        {
            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => { await _valueBinder.SetValueAsync(_message, CancellationToken.None); });
            Assert.Equal("A 'To' address must be specified for the message.", ex.Message);

            _message.AddTo("test@test.com");
            ex = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => { await _valueBinder.SetValueAsync(_message, CancellationToken.None); });
            Assert.Equal("A 'From' address must be specified for the message.", ex.Message);
        }

        [Fact]
        public async Task SetValueAsync_InvalidMessage_Throws()
        {
            Web web = new Web("1234");
            SendGridMessage message = new SendGridMessage
            {
                From = new System.Net.Mail.MailAddress("test@test.com")
            };
            message.AddTo("test@test.com");

            SendGridValueBinder binder = new SendGridValueBinder(web, message);

            // SendGrid isn't mockable, so we're just catching their exception as proof
            // that we called send
            await Assert.ThrowsAsync<Exceptions.InvalidApiRequestException>(
                async () => { await binder.SetValueAsync(message, CancellationToken.None); });
        }
    }
}
