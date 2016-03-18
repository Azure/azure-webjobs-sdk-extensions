// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.NotificationHubs;
using Microsoft.Azure.WebJobs.Extensions.NotificationHubs;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.NotificationHub
{
    public class ConverterTests
    {
        [Fact]
        public void Converter_JsonString_Valid()
        {
            Converter converter = new Converter();
            string messageProperties = "{\"message\":\"Hello from Node! \",\"location\":\"Redmond\"}";
            TemplateNotification templateNotification = converter.BuildTemplateNotificationFromJsonString(messageProperties);
            Assert.NotNull(templateNotification);
        }

        [Fact]
        public void Converter_JsonString_Invalid()
        {
            Converter converter = new Converter();
            string messageProperties = "{\"message\":\"location\":\"Redmond\"}";
            Type expectedExceptionType = typeof(JsonReaderException);
            var exception = Assert.ThrowsAny<Exception>(() => converter.BuildTemplateNotificationFromJsonString(messageProperties));
            Assert.Equal(expectedExceptionType, exception.GetType());
        }

        [Fact]
        public void Converter_DictionaryTemplateProperties_Valid()
        {
            Converter converter = new Converter();
            Dictionary<string, string> templateProperties = new Dictionary<string, string>();
            templateProperties["message"] = "Hello";
            templateProperties["location"] = "Redmond";
            TemplateNotification templateNotification = converter.BuildTemplateNotificationFromDictionary(templateProperties);
            Assert.NotNull(templateNotification);
        }
    }
}