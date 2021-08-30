// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SendGrid.Helpers.Mail;
using WebJobsSandbox;

namespace ExtensionsSample
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            string filesTestPath = @"c:\temp\files";

            // Add or remove types from this list to choose which functions will 
            // be indexed by the JobHost.
            // To run some of the other samples included, add their types to this list
            var typeLocator = new SamplesTypeLocator(
                typeof(FileSamples),
                typeof(TimerSamples));

            var builder = new HostBuilder()
               .UseEnvironment("Development")
               .ConfigureWebJobs(webJobsBuilder =>
               {
                   webJobsBuilder
                   .AddAzureStorageCoreServices()
                   .AddAzureStorage()
                   .AddFiles(o =>
                   {
                       o.RootPath = filesTestPath;
                   })
                   .AddTimers()
                   .AddMobileApps()
                   .AddTwilioSms()
                   .AddCosmosDB()
                   .AddSendGrid(o =>
                   {
                       o.ToAddress = new EmailAddress("admin@webjobssamples.com", "WebJobs Extensions Samples");
                       o.FromAddress = new EmailAddress("samples@webjobssamples.com", "WebJobs Extensions Samples");
                   });
               })
               .ConfigureLogging(b =>
               {
                   b.SetMinimumLevel(LogLevel.Debug);
                   b.AddConsole();
               })
               .ConfigureServices(s =>
               {
                   s.AddSingleton<ITypeLocator>(typeLocator);
                   s.AddAzureClientsCore();
               })
               .UseConsoleLifetime();

            EnsureSampleDirectoriesExist(filesTestPath);

            var host = builder.Build();
            using (host)
            {
                // Some direct invocations to demonstrate various binding scenarios
                var jobHost = (JobHost)host.Services.GetService<IJobHost>();
                await jobHost.CallAsync(typeof(FileSamples).GetMethod("ReadWrite"));

                await host.RunAsync();
            }
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