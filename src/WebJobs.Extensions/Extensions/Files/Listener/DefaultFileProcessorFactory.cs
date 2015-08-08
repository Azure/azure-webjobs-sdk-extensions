using System.Threading;

namespace Microsoft.Azure.WebJobs.Extensions.Files.Listener
{
    internal class DefaultFileProcessorFactory : IFileProcessorFactory
    {
        public FileProcessor CreateFileProcessor(FileProcessorFactoryContext context)
        {
            return new FileProcessor(context);
        }
    }
}
