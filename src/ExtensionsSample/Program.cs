// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net.Mail;
using Microsoft.AspNet.WebHooks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions;
using Microsoft.Azure.WebJobs.Extensions.Files;
using Microsoft.Azure.WebJobs.Extensions.WebHooks;
using Microsoft.Azure.WebJobs.Host;

namespace ExtensionsSample
{
    public static class Program
    {
        public static void Main(string[] args)
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
            }
            EnsureSampleDirectoriesExist(filesConfig.RootPath);
            config.UseFiles(filesConfig);

            config.UseTimers();
            config.UseSample();
            config.UseCore();
            var sendGridConfiguration = new SendGridConfiguration()
            {
                ToAddress = "admin@webjobssamples.com",
                FromAddress = new MailAddress("samples@webjobssamples.com", "WebJobs Extensions Samples")
            };
            config.UseSendGrid(sendGridConfiguration);

            ConfigureTraceMonitor(config, sendGridConfiguration);

            WebHooksConfiguration webHooksConfig = new WebHooksConfiguration();
            webHooksConfig.UseReceiver<GitHubWebHookReceiver>();
            config.UseWebHooks(webHooksConfig);

            JobHost host = new JobHost(config);

            host.Call(typeof(MiscellaneousSamples).GetMethod("ExecutionContext"));
            host.Call(typeof(FileSamples).GetMethod("ReadWrite"));
            host.Call(typeof(SampleSamples).GetMethod("Sample_BindToStream"));
            host.Call(typeof(SampleSamples).GetMethod("Sample_BindToString"));
            host.Call(typeof(TableSamples).GetMethod("CustomBinding"));

            host.RunAndBlock();
        }

        /// <summary>
        /// Set up monitoring + notifications for WebJob errors.
        /// </summary>
        private static void ConfigureTraceMonitor(JobHostConfiguration config, SendGridConfiguration sendGridConfiguration)
        {
            var notifier = new TraceNotifier(sendGridConfiguration);

            var traceMonitor = new TraceMonitor(TraceLevel.Error)
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
