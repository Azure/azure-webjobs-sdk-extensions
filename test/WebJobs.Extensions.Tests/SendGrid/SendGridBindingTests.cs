// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Reflection;
using System.Threading;
using Microsoft.Azure.WebJobs.Extensions;
using Microsoft.Azure.WebJobs.Host.Bindings;
using SendGrid;
using Xunit;

using SendGridBinding = Microsoft.Azure.WebJobs.Extensions.SendGridAttributeBindingProvider.SendGridBinding;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.SendGrid
{
    public class SendGridBindingTests
    {
        [Fact]
        public void CreateDefaultMessage_CreatesExpectedMessage()
        {
            ParameterInfo parameter = GetType().GetMethod("TestMethod", BindingFlags.Static|BindingFlags.NonPublic).GetParameters().First();
            SendGridAttribute attribute = new SendGridAttribute
            {
                To = "{Param1}",
                Subject = "Test {Param2}",
                Text = "Test {Param3}"
            };
            SendGridConfiguration config = new SendGridConfiguration
            {
                ApiKey = "12345",
                FromAddress = new MailAddress("test2@test.com", "Test2"),
                ToAddress = "test3@test.com"
            };
            Dictionary<string, Type> contract = new Dictionary<string, Type>();
            contract.Add("Param1", typeof(string));
            contract.Add("Param2", typeof(string));
            contract.Add("Param3", typeof(string));
            BindingProviderContext context = new BindingProviderContext(parameter, contract, CancellationToken.None);

            SendGridBinding binding = new SendGridBinding(parameter, attribute, config, context);
            Dictionary<string, object> bindingData = new Dictionary<string, object>();
            bindingData.Add("Param1", "test1@test.com");
            bindingData.Add("Param2", "Value2");
            bindingData.Add("Param3", "Value3");
            SendGridMessage message = binding.CreateDefaultMessage(bindingData);

            Assert.Same(config.FromAddress, message.From);
            Assert.Equal("test1@test.com", message.To.Single().Address);
            Assert.Equal("Test Value2", message.Subject);
            Assert.Equal("Test Value3", message.Text);

            // If no To value specified, verify it is defaulted from config
            attribute = new SendGridAttribute
            {
                Subject = "Test {Param2}",
                Text = "Test {Param3}"
            };
            binding = new SendGridBinding(parameter, attribute, config, context);
            message = binding.CreateDefaultMessage(bindingData);
            Assert.Equal("test3@test.com", message.To.Single().Address);
        }

        private static void TestMethod([SendGrid] SendGridMessage message) { }
    }
}
