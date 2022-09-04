// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Extensions.Files;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Files.Config
{
    public class FilesOptionsTests
    {
        [Fact]
        public void Constructor_Defaults()
        {
            Environment.SetEnvironmentVariable("HOME", null);

            var options = new FilesOptions();
            Assert.Null(options.RootPath);

            Environment.SetEnvironmentVariable("HOME", @"D:\home");
            options = new FilesOptions();
            Assert.Equal(@"D:\home\data", options.RootPath);

            Environment.SetEnvironmentVariable("HOME", null);
        }
    }
}
