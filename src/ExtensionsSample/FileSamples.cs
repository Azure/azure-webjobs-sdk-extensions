using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.Azure.WebJobs.Extensions.Files;

namespace WebJobsSandbox
{
    public static class FileSamples
    {
        public static void ImportFile(
            [FileTrigger(@"data\import\{name}", autoDelete: true, filter: "*.dat")] FileStream file,
            [Blob(@"processed/{name}")] CloudBlockBlob output,
            string name,
            TextWriter log)
        {
            output.UploadFromStream(file);
            file.Close();

            log.WriteLine(string.Format("Processed input file '{0}'!", name));
        }
    }
}
