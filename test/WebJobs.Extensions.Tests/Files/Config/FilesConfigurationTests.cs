// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Extensions.Files;
using Microsoft.Azure.WebJobs.Extensions.Files.Listener;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Files.Config
{
    public class FilesConfigurationTests
    {
        [Fact]
        public void Constructor_Defaults()
        {
            Environment.SetEnvironmentVariable("HOME", null);

            FilesConfiguration config = new FilesConfiguration();
            Assert.Null(config.RootPath);
            Assert.Equal(typeof(DefaultFileProcessorFactory), config.ProcessorFactory.GetType());

            Environment.SetEnvironmentVariable("HOME", @"D:\home");
            config = new FilesConfiguration();
            Assert.Equal(@"D:\home\data", config.RootPath);

            Environment.SetEnvironmentVariable("HOME", null);
        }
    }
}
