﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Azure.NotificationHubs;
using Microsoft.Azure.WebJobs.Extensions.NotificationHub;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.NotificationHub
{
    public class ConverterTests
    {
        [Fact]
        public void Converter_JsonString_Valid()
        {
            TemplateNotification templateNotification = Converter.BuildTemplateNotificationFromJsonString(GetTemplatePropertiesJsonString());
            Assert.NotNull(templateNotification);
            Assert.True(VerifyTemplate(templateNotification));
        }

        [Fact]
        public void Converter_JsonString_Invalid()
        {
            string messageProperties = "{\"message\":\"location\":\"Redmond\"}";
            Type expectedExceptionType = typeof(JsonReaderException);
            var exception = Assert.ThrowsAny<Exception>(() => Converter.BuildTemplateNotificationFromJsonString(messageProperties));
            Assert.Equal(expectedExceptionType, exception.GetType());
        }

        [Fact]
        public void Converter_DictionaryTemplateProperties_Valid()
        {
            TemplateNotification templateNotification = Converter.BuildTemplateNotificationFromDictionary(GetTemplateProperties());
            Assert.NotNull(templateNotification);
            Assert.True(VerifyTemplate(templateNotification));
        }

        private static Dictionary<string, string> GetTemplateProperties()
        {
            Dictionary<string, string> templateProperties = new Dictionary<string, string>();
            templateProperties["message"] = "Hello";
            templateProperties["location"] = "Redmond";
            return templateProperties;
        }

        private static string GetTemplatePropertiesJsonString()
        {
            return JsonConvert.SerializeObject(GetTemplateProperties());
        }

        public static bool VerifyTemplate(TemplateNotification templateNotification)
        {
            FieldInfo templatePropertiesProperty = templateNotification.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance).Single(pi => pi.Name == "templateProperties");
            IDictionary<string, string> actualTemplateProperties = (IDictionary<string, string>)templatePropertiesProperty.GetValue(templateNotification);
            return AreTemplatePropertiesEqual(GetTemplateProperties(), actualTemplateProperties);
        }


        public static bool AreTemplatePropertiesEqual(IDictionary<string, string> expectedProperties, IDictionary<string, string> actualProperties)
        {
            if (expectedProperties.Count == actualProperties.Count)
            {
                return actualProperties.Keys.All(key => expectedProperties.ContainsKey(key) && (actualProperties[key] == expectedProperties[key]));
            }
            return false;
        }
    }
}
