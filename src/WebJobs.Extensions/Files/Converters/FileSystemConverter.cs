using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Converters;

namespace WebJobs.Extensions.Files.Converters
{
    internal class FileSystemEventConverter<TOutput> : IAsyncConverter<FileSystemEventArgs, TOutput>
    {
        public Task<TOutput> ConvertAsync(FileSystemEventArgs input, CancellationToken cancellationToken)
        {
            object result = null;

            if (typeof(TOutput) == typeof(FileStream))
            {
                result = File.OpenRead(input.FullPath);
            }
            if (typeof(TOutput) == typeof(FileInfo))
            {
                result = new FileInfo(input.FullPath);
            }
            else if (typeof(TOutput) == typeof(byte[]))
            {
                result = File.ReadAllBytes(input.FullPath);
            }
            else if (typeof(TOutput) == typeof(string))
            {
                result = File.ReadAllText(input.FullPath);
            }

            return Task.FromResult<TOutput>((TOutput)result);
        }
    }
}
