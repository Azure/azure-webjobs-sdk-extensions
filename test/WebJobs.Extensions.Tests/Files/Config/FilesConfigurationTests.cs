using System;
using WebJobs.Extensions.Files;
using WebJobs.Extensions.Files.Listener;
using Xunit;

namespace WebJobs.Extensions.Tests.Files.Config
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
