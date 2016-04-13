// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.Azure.ApiHub;
using Microsoft.Azure.WebJobs;
using Microsoft.WindowsAzure.Storage.Blob;

namespace ExtensionsSample.Samples
{
    // To use the ApiHubSamples:
    // 1. Add an AzureWebJobsDropBox app setting for your ApiHub DropBox connection, The format should be: Endpoint={endpoint};Scheme={scheme};AccessToken={accesstoken}
    // 2. Call ApiHubSamples.UseApiHub(config) in Program.cs
    // 3. Add typeof(ApiHubSamples) to the SamplesTypeLocator in Program.cs
    public static class ApiHubSamples
    {
        public static void UseApiHub(JobHostConfiguration config)
        {
            string apiHubConnectionString = null;

            // ApiHub for dropbox is enabled if the AzureWebJobsDropBox environment variable is set.           
            // The format should be: Endpoint={endpoint};Scheme={scheme};AccessToken={accesstoken}
            // otherwise use the local file system
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AzureWebJobsDropBox")))
            {
                apiHubConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsDropBox");
            }
            else
            {
                apiHubConnectionString = "UseLocalFileSystem=true;Path=" + Path.GetTempPath() + "ApiHubDropBox";
            }

            if (!string.IsNullOrEmpty(apiHubConnectionString))
            {
                var apiHubConfig = new ApiHubConfiguration();
                apiHubConfig.AddKeyPath("dropbox", apiHubConnectionString);
                config.UseApiHub(apiHubConfig);

                // Create some initialization files.
                var root = ItemFactory.Parse(apiHubConnectionString);
                var file = root.GetFileReferenceAsync("test/file1.txt", true).GetAwaiter().GetResult();
                file.WriteAsync(new byte[] { 0, 1, 2, 3 });
            }
        }

        // When new files arrive in dropbox's test folder, they are uploaded to dropbox's testout folder.
        public static void Trigger(
            [ApiHubFileTrigger("dropbox", @"test/{name}", PollIntervalInSeconds = 5)] Stream input,
            [ApiHubFile("dropbox", @"testout/{name}", FileAccess.Write)] StreamWriter output)
        {
            using (StreamReader reader = new StreamReader(input))
            {
                string text = reader.ReadToEnd();
                output.Write(text);
            }
        }

        public static void BindToStreamOutput(
            [ApiHubFile("dropbox", "import/file1.txt")] string input,
            [ApiHubFile("dropbox", "import/file1-out.txt", FileAccess.Write)] Stream output)
        {
            StreamWriter sw = new StreamWriter(output);
            
            sw.Write(input);
            sw.Flush();
        }

        public static void BindToStreamOutputString(
            [ApiHubFile("dropbox", "import/file1.txt")] string input,
            [ApiHubFile("dropbox", "import/file1-out.txt", FileAccess.Write)] out string output)
        {
            input = input + "123456";
            output = input;
        }

        public static void BindToStreamOutputByteArray(
            [ApiHubFile("dropbox", "import/file1.txt")] byte[] input,
            [ApiHubFile("dropbox", "import/file1-out.txt", FileAccess.Write)] out byte[] output)
        {
            output = input;
        }

        // Every time the timer fires, the file in dropbox will be updated with the current time.
        public static void Heartbeat(
            [TimerTrigger("00:00:30")] TimerInfo timerInfo,
            [ApiHubFile("dropbox", "test/timer.txt", FileAccess.Write)] out string output)
        {
            // TODO: Update this sample when FileAppend is supported for ApiHub.
            string input = "Heartbeat timer triggered at " + DateTime.Now;
            output = "Via Webjobs:" + input;
        }

        // When new files arrive in the dropbox's "import" directory, they are uploaded to a blob
        public static void ImportFile(
            [ApiHubFileTrigger("dropbox", "import/{name}")] TextReader tr,
            [Blob(@"processed/{name}")] CloudBlockBlob output,
            string name,
            TextWriter log)
        {
            // TODO: Update this sample when autuDelete is supported for ApiHub
            string input = tr.ReadToEnd();

            output.UploadText(input);

            log.WriteLine(string.Format("Processed input file '{0}'!", name));
        }
    }
}
