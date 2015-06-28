using System;
using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Files;
using WebJobsSandbox;

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
            EnsureSampleDirectoriesExist(filesConfig.RootPath);
            config.UseFiles(filesConfig);

            config.UseTimers();

            config.UseSample();

            JobHost host = new JobHost(config);

            host.Call(typeof(FileSamples).GetMethod("ReadWrite"));
            host.Call(typeof(SampleSamples).GetMethod("Sample_BindToStream"));
            host.Call(typeof(SampleSamples).GetMethod("Sample_BindToString"));

            host.RunAndBlock();
        }

        private static void EnsureSampleDirectoriesExist(string rootFilesPath)
        {
            // Ensure all the directories referenced by the file sample bindings
            // exist
            Directory.CreateDirectory(rootFilesPath);
            Directory.CreateDirectory(Path.Combine(rootFilesPath, "import"));
            Directory.CreateDirectory(Path.Combine(rootFilesPath, "cache"));
            Directory.CreateDirectory(Path.Combine(rootFilesPath, "convert"));
            Directory.CreateDirectory(Path.Combine(rootFilesPath, "converted"));

            File.WriteAllText(Path.Combine(rootFilesPath, "input.txt"), "WebJobs SDK Extensions!");
        }
    }
}
