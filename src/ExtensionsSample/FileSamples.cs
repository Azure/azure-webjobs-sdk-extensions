using System;
using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Files;
using Microsoft.Azure.WebJobs.Extensions.Timers;
using Microsoft.WindowsAzure.Storage.Blob;

namespace WebJobsSandbox
{
    public static class FileSamples
    {
        // When new files arrive in the "import" directory, they are uploaded to a blob
        // container then deleted.
        public static void ImportFile(
            [FileTrigger(@"import\{name}", "*.dat", autoDelete: true)] Stream file,
            [Blob(@"processed/{name}")] CloudBlockBlob output,
            string name,
            TextWriter log)
        {
            output.UploadFromStream(file);
            file.Close();

            log.WriteLine(string.Format("Processed input file '{0}'!", name));
        }

        // When files are created or modified in the "cache" directory, this job will be triggered.
        public static void ChangeWatcher(
            [FileTrigger(@"cache\{name}", "*.txt", WatcherChangeTypes.Created | WatcherChangeTypes.Changed)] string file,
            FileSystemEventArgs fileTrigger,
            TextWriter log)
        {
            log.WriteLine(string.Format("Processed input file '{0}'!", fileTrigger.Name));
        }

        // Drop a file in the "convert" directory, and this function will reverse it
        // the contents and write the file to the "converted" directory.
        public static void Converter(
            [FileTrigger(@"convert\{name}", "*.txt", autoDelete: true)] string file,
            [File(@"converted\{name}")] out string converted)
        {
            char[] arr = file.ToCharArray();
            Array.Reverse(arr);
            converted = new string(arr);
        }

        // Every time the timer fires, this file will update a file with the current time.
        public static void Heartbeat(
            [TimerTrigger("*/5 * * * * *")] TimerInfo timerInfo,
            [File(@"heartbeat.txt", FileAccess.Write, FileMode.Append)] Stream file)
        {
            using (StreamWriter sw = new StreamWriter(file))
            {
                sw.WriteLine("Heartbeat timer triggered at " + DateTime.Now);
            }
        }

        public static void ReadWrite(
            [File(@"input.txt", FileAccess.Read, FileMode.OpenOrCreate)] Stream input,
            [File(@"output.txt", FileAccess.Write, FileMode.Append)] Stream output)
        {
            input.CopyTo(output);
        }
    }
}
