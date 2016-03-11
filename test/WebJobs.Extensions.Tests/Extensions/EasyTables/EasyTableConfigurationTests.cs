// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Extensions.EasyTables;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.EasyTables
{
    public class EasyTableConfigurationTests
    {
        private const string AppSettingKey = EasyTablesConfiguration.AzureWebJobsMobileAppUriName;
        private const string EnvironmentKey = AppSettingKey + "_environment";
        private const string NeitherKey = AppSettingKey + "_neither";

        [Fact]
        public void Resolve_UsesAppSettings_First()
        {
            // Arrange
            SetEnvironment(AppSettingKey);

            // Act
            var mobileAppUri = EasyTablesConfiguration.Resolve(AppSettingKey);

            // Assert            
            Assert.Equal("https://fromappsettings/", mobileAppUri.ToString());

            ClearEnvironment();
        }

        [Fact]
        public void Resolve_UsesEnvironment_Second()
        {
            // Arrange
            SetEnvironment(EnvironmentKey);

            // Act
            var mobileAppUri = EasyTablesConfiguration.Resolve(EnvironmentKey);

            // Assert
            Assert.Equal("https://fromenvironment/", mobileAppUri.ToString());

            ClearEnvironment();
        }

        [Fact]
        public void Resolve_FallsBackToNull()
        {
            // Arrange
            ClearEnvironment();

            // Act
            var mobileAppUri = EasyTablesConfiguration.Resolve(NeitherKey);

            // Assert            
            Assert.Null(mobileAppUri);
        }

        private static void SetEnvironment(string key)
        {
            Environment.SetEnvironmentVariable(key, "https://fromenvironment/");
        }

        private static void ClearEnvironment()
        {
            Environment.SetEnvironmentVariable(AppSettingKey, null);
            Environment.SetEnvironmentVariable(EnvironmentKey, null);
            Environment.SetEnvironmentVariable(NeitherKey, null);
        }
    }
}
