using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Files;

namespace ExtensionsSample
{
    class Program
    {
        static void Main(string[] args)
        {
            JobHostConfiguration config = new JobHostConfiguration();

            FilesConfiguration filesConfig = new FilesConfiguration
            {
                RootPath = @"c:\temp\files"
            };
            config.UseFiles(filesConfig);

            config.UseTimers();

            JobHost host = new JobHost(config);
            host.RunAndBlock();
        }
    }
}
