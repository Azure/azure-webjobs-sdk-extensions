// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using ExtensionsSample.Samples;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions;
using Microsoft.Azure.WebJobs.Extensions.Files;
using Microsoft.Azure.WebJobs.Extensions.SendGrid;
using Microsoft.Azure.WebJobs.Host;
using SendGrid.Helpers.Mail;
using WebJobsSandbox;

namespace ExtensionsSample
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            JobHostConfiguration config = new JobHostConfiguration();

            config.HostId = "hostid1234";
            config.StorageConnectionString = null;
            config.DashboardConnectionString = null;
            SasConfig.SaSConnection = "https://mltest176.blob.core.windows.net/azure-webjobs-hosts?st=2017-11-10T10%3A42%3A00Z&se=2018-04-19T12%3A00%3A00Z&sp=rwdl&sv=2017-04-17&sr=c&sig=rvNZEnB9rTmEwjb%2BTKWHTXHrDHqB90XjU%2FzdO4Zyh30%3D";

            FilesConfiguration filesConfig = new FilesConfiguration();

            // See https://github.com/Azure/azure-webjobs-sdk/wiki/Running-Locally for details
            // on how to set up your local environment
            if (config.IsDevelopment)
            {
                config.UseDevelopmentSettings();
                filesConfig.RootPath = @"c:\temp\files";
            }

            config.UseTimers();

            JobHost host = new JobHost(config);

            // Add or remove types from this list to choose which functions will 
            // be indexed by the JobHost.
            // To run some of the other samples included, add their types to this list
            config.TypeLocator = new SamplesTypeLocator(
                typeof(TimerSamples));
        
            host.RunAndBlock();
        }

        /// <summary>
        /// Set up monitoring + notifications for WebJob errors. This shows how to set things up
        /// manually on startup. You can also use <see cref="ErrorTriggerAttribute"/> to designate
        /// error handler functions.
        /// </summary>
        private static void ConfigureTraceMonitor(JobHostConfiguration config, SendGridConfiguration sendGridConfiguration)
        {
            var notifier = new ErrorNotifier(sendGridConfiguration);

            var traceMonitor = new TraceMonitor()
                .Filter(new SlidingWindowTraceFilter(TimeSpan.FromMinutes(5), 3))
                .Filter(p =>
                {
                    FunctionInvocationException functionException = p.Exception as FunctionInvocationException;
                    return p.Level == TraceLevel.Error && functionException != null &&
                           functionException.MethodName == "ExtensionsSample.FileSamples.ImportFile";
                }, "ImportFile Job Failed")
                .Subscribe(notifier.WebNotify, notifier.EmailNotify)
                .Throttle(TimeSpan.FromMinutes(30));

            config.Tracing.Tracers.Add(traceMonitor);
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