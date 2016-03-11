// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Extensions.EasyTables;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.EasyTables
{
    public class EasyTableConfigurationTests
    {
        [Fact]
        public void Resolve_UsesAppSettings_First()
        {
            // Arrange
            SetEnvironment();

            // Act
            var mobileAppUri = EasyTablesConfiguration.Resolve(EasyTablesConfiguration.AzureWebJobsMobileAppUriName);

            // Assert            
            Assert.Equal("https://fromappsettings/", mobileAppUri.ToString());

            ClearEnvironment();
        }

        [Fact(Skip = "No good way to mock ConfigurationManager. Manually verified.")]
        public void Resolve_UsesEnvironment_Second()
        {
            // Arrange
            SetEnvironment();

            // Act
            var mobileAppUri = EasyTablesConfiguration.Resolve(EasyTablesConfiguration.AzureWebJobsMobileAppUriName);

            // Assert
            // The conversion to URI adds a trailing slash
            Assert.Equal("https://fromenvironment/", mobileAppUri.ToString());

            ClearEnvironment();
        }

        [Fact(Skip = "No good way to mock ConfigurationManager. Manually verified.")]
        public void Resolve_FallsBackToNull()
        {
            // Arrange
            ClearEnvironment();

            // Act
            var mobileAppUri = EasyTablesConfiguration.Resolve(EasyTablesConfiguration.AzureWebJobsMobileAppUriName);

            // Assert            
            Assert.Null(mobileAppUri);
        }

        private static void SetEnvironment()
        {
            Environment.SetEnvironmentVariable(EasyTablesConfiguration.AzureWebJobsMobileAppUriName, "https://fromEnvironment/");
        }

        private static void ClearEnvironment()
        {
            Environment.SetEnvironmentVariable(EasyTablesConfiguration.AzureWebJobsMobileAppUriName, null);
        }
    }
}
