﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.WindowsAzure.Storage.Blob;

namespace ExtensionsSample.Samples
{
    public static class ApiHubSamples
    {
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

        public static void Writer(
            [ApiHubFile("dropbox", "test/file1.txt")] Stream input,
            [ApiHubFile("dropbox", "test/file1-out.txt", FileAccess.Write)] StreamWriter output)
        {
            using (StreamReader reader = new StreamReader(input))
            {
                string text = reader.ReadToEnd();
                output.Write(text);
            }
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
