using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Files;
using Microsoft.WindowsAzure.Storage.Blob;

namespace WebJobsSandbox
{
    public static class FileSamples
    {
        public static void ImportFile(
            [FileTrigger(@"import\{name}", "*.dat", autoDelete: true)] FileStream file,
            [Blob(@"processed/{name}")] CloudBlockBlob output,
            string name,
            TextWriter log)
        {
            output.UploadFromStream(file);
            file.Close();

            log.WriteLine(string.Format("Processed input file '{0}'!", name));
        }

        public static void ChangeWatcher(
            [FileTrigger(@"cache\{name}", "*.txt", WatcherChangeTypes.Created | WatcherChangeTypes.Changed)] string file,
            FileSystemEventArgs fileTrigger,
            TextWriter log)
        {
            log.WriteLine(string.Format("Processed input file '{0}'!", fileTrigger.Name));
        }
    }
}
