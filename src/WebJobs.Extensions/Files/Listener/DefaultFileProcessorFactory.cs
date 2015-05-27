using System.Threading;

namespace WebJobs.Extensions.Files.Listener
{
    internal class DefaultFileProcessorFactory : IFileProcessorFactory
    {
        public FileProcessor CreateFileProcessor(FileProcessorFactoryContext context, CancellationTokenSource cancellationTokenSource)
        {
            return new FileProcessor(context, cancellationTokenSource);
        }
    }
}
