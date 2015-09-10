using System;
using System.Diagnostics;
using System.IO;
using System.Net.Mail;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Files;
using Microsoft.Azure.WebJobs.Extensions;
using Microsoft.Azure.WebJobs.Extensions.WebHooks;

namespace ExtensionsSample
{
    class Program
    {
        static void Main(string[] args)
        {
            JobHostConfiguration config = new JobHostConfiguration();

            config.Tracing.ConsoleLevel = TraceLevel.Verbose;

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
            config.UseCore();
            config.UseSendGrid(new SendGridConfiguration()
            {
                FromAddress = new MailAddress("orders@webjobssamples.com", "Order Processor")
            });

            JobHost host = new JobHost(config);
            config.UseWebHooks(host);

            host.Call(typeof(MiscellaneousSamples).GetMethod("ExecutionContext"));
            host.Call(typeof(FileSamples).GetMethod("ReadWrite"));
            host.Call(typeof(SampleSamples).GetMethod("Sample_BindToStream"));
            host.Call(typeof(SampleSamples).GetMethod("Sample_BindToString"));
            host.Call(typeof(TableSamples).GetMethod("CustomBinding"));

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
