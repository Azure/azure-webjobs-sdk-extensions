﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Files;
using Microsoft.Azure.WebJobs.Extensions.SendGrid;
using SendGrid.Helpers.Mail;
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
            config.UseMobileApps();
            config.UseTwilioSms();
            config.UseCosmosDB();

            var sendGridConfiguration = new SendGridConfiguration()
            {
                ToAddress = new EmailAddress("admin@webjobssamples.com", "WebJobs Extensions Samples"),
                FromAddress = new EmailAddress("samples@webjobssamples.com", "WebJobs Extensions Samples")
            };
            config.UseSendGrid(sendGridConfiguration);

            EnsureSampleDirectoriesExist(filesConfig.RootPath);

            JobHost host = new JobHost(config);

            // Add or remove types from this list to choose which functions will 
            // be indexed by the JobHost.
            // To run some of the other samples included, add their types to this list
            config.TypeLocator = new SamplesTypeLocator(
                typeof(FileSamples),
                typeof(MiscellaneousSamples),
                typeof(SampleSamples),
                typeof(TableSamples),
                typeof(TimerSamples));

            // Some direct invocations to demonstrate various binding scenarios
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