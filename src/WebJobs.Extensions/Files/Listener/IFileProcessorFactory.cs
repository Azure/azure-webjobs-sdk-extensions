using System.Threading;

namespace Microsoft.Azure.WebJobs.Extensions.Files.Listener
{
    /// <summary>
    /// Factory interface for creating <see cref="FileProcessor"/> instances. This factory pattern allows
    /// different FileProcessors to be used for different job functions.
    /// </summary>
    public interface IFileProcessorFactory
    {
        /// <summary>
        /// Create a <see cref="FileProcessor"/> for the specified inputs.
        /// </summary>
        /// <param name="context">The context to use.</param>
        /// <param name="cancellationTokenSource">The <see cref="CancellationTokenSource"/> to use.</param>
        /// <returns>The <see cref="FileProcessor"/></returns>
        FileProcessor CreateFileProcessor(FileProcessorFactoryContext context, CancellationTokenSource cancellationTokenSource);
    }
}
