﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Mail;
using ExtensionsSample.Samples;
using Microsoft.AspNet.WebHooks;
using Microsoft.Azure.ApiHub;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions;
using Microsoft.Azure.WebJobs.Extensions.ApiHub;
using Microsoft.Azure.WebJobs.Extensions.Files;
using Microsoft.Azure.WebJobs.Extensions.SendGrid;
using Microsoft.Azure.WebJobs.Extensions.WebHooks;
using Microsoft.Azure.WebJobs.Host;
using WebJobsSandbox;

namespace ExtensionsSample
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            JobHostConfiguration config = new JobHostConfiguration();
            FilesConfiguration filesConfig = new FilesConfiguration();

            // See https://github.com/Azure/azure-webjobs-sdk/wiki/Running-Locally for details
            // on how to set up your local environment
            if (config.IsDevelopment)
            {
                config.UseDevelopmentSettings();
                filesConfig.RootPath = @"c:\temp\files";
            }

            config.UseFiles(filesConfig);
            config.UseTimers();
            config.UseSample();
            config.UseEasyTables();
            config.UseCore();
            config.UseDocumentDB();
            config.UseNotificationHubs();
            var sendGridConfiguration = new SendGridConfiguration()
            {
                ToAddress = "admin@webjobssamples.com",
                FromAddress = new MailAddress("samples@webjobssamples.com", "WebJobs Extensions Samples")
            };
            config.UseSendGrid(sendGridConfiguration);

            bool apiHubEnabled = CheckForApiHub(config);

            ConfigureTraceMonitor(config, sendGridConfiguration);

            EnsureSampleDirectoriesExist(filesConfig.RootPath);

            WebHooksConfiguration webHooksConfig = new WebHooksConfiguration();
            webHooksConfig.UseReceiver<GitHubWebHookReceiver>();
            config.UseWebHooks(webHooksConfig);

            JobHost host = new JobHost(config);

            // Add or remove types from this list to choose which functions will 
            // be indexed by the JobHost.
            config.TypeLocator = new SamplesTypeLocator(
                typeof(ErrorMonitoringSamples),
                typeof(FileSamples),
                typeof(MiscellaneousSamples),
                typeof(SampleSamples),
                typeof(SendGridSamples),
                typeof(TableSamples),
                typeof(TimerSamples),
                typeof(WebHookSamples),
                typeof(ApiHubSamples));
            
            host.Call(typeof(MiscellaneousSamples).GetMethod("ExecutionContext"));
            host.Call(typeof(FileSamples).GetMethod("ReadWrite"));
            host.Call(typeof(SampleSamples).GetMethod("Sample_BindToStream"));
            host.Call(typeof(SampleSamples).GetMethod("Sample_BindToString"));
            host.Call(typeof(TableSamples).GetMethod("CustomBinding"));

            if (apiHubEnabled)
            {
                host.Call(typeof(ApiHubSamples).GetMethod("Writer"));
            }

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

        private static bool CheckForApiHub(JobHostConfiguration config)
        {
            string dropboxKey = null;

            // ApiHub for dropbox is enabled if the dropbox-connectionkey.txt exists in the current folder
            // or the dropbox environment variable is set.
            // The format should be: Endpoint={endpoint};Scheme={scheme};AccessToken={accesstoken}
            if (File.Exists("dropbox-connectionkey.txt"))
            {
                dropboxKey = File.ReadAllText("dropbox-connectionkey.txt");
            }
            else
            {
                dropboxKey = Environment.GetEnvironmentVariable("dropbox");
            }

            if (!string.IsNullOrEmpty(dropboxKey))
            {
                var apiHubConfig = new ApiHubConfiguration();
                apiHubConfig.AddKeyPath("dropbox", dropboxKey);
                config.UseApiHub(apiHubConfig);

                // Create some initialization files.
                var root = ItemFactory.Parse(dropboxKey);
                var file = root.CreateFileAsync("test/file1.txt", true).GetAwaiter().GetResult();
                file.WriteAsync(new byte[] { 0, 1, 2, 3 });
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
