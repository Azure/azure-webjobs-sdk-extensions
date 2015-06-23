using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Files;

namespace ExtensionsSample
{
    class Program
    {
        static void Main(string[] args)
        {
            JobHostConfiguration config = new JobHostConfiguration();

            // Set to a short polling interval to facilitate local
            // debugging. You wouldn't want to run prod this way.
            config.Queues.MaxPollingInterval = TimeSpan.FromSeconds(2);

            FilesConfiguration filesConfig = new FilesConfiguration();
            if (string.IsNullOrEmpty(filesConfig.RootPath))
            {
                // when running locally, set this to a valid directory
                filesConfig.RootPath = @"c:\temp\files";
            };
            config.UseFiles(filesConfig);

            config.UseTimers();

            config.UseSample();

            JobHost host = new JobHost(config);

            host.Call(typeof(SampleSamples).GetMethod("Sample_BindToStream"));
            host.Call(typeof(SampleSamples).GetMethod("Sample_BindToString"));

            host.RunAndBlock();
        }
    }
}
