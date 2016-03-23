// Copyright (c) .NET Foundation. All rights reserved.
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
            [ApiHubFileTrigger("dropbox", "test/{name}")] TextReader tr,
            [ApiHubFile("dropbox", "testout/{name}")] out string output)
        {
            string input = tr.ReadToEnd();
            output = "Via Webjobs:" + input;
        }

        public static void Writer(
            [ApiHubFile("dropbox", "test/file1.txt")] TextReader tr,
            [ApiHubFile("dropbox", "test/file1-out.txt")] out string output)
        {
            string input = tr.ReadToEnd();
            output = "Via Webjobs:" + input;
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
