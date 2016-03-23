using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.ApiHub;

namespace Microsoft.Azure.WebJobs.Extensions.ApiHub.Common
{
    internal class ApiHubFile : IFileStreamProvider
    {
        private readonly IFileItem _fileSource;

        public string Path
        {
            get
            {
                return _fileSource.Path;
            }
        }

        public ApiHubFile(IFolderItem rootFolder, string path)
        {
            _fileSource = rootFolder.CreateFileAsync(path, true).GetAwaiter().GetResult();
        }

        public ApiHubFile(IFileItem fileSource)
        {
            _fileSource = fileSource;
        }

        public async Task<Stream> OpenReadStreamAsync()
        {
            var bytes = await _fileSource.ReadAsync();

            MemoryStream ms = new MemoryStream(bytes);
            
            return ms;            
        }

        public Task<Tuple<Stream, Func<Task>>> OpenWriteStreamAsync()
        {
            var stream = new MemoryStream();
            
            Func<Task> onClose = async () =>
            {
                stream.Position = 0;
                var bytes = stream.ToArray();
                await _fileSource.WriteAsync(bytes);
            };

            return Task.FromResult(Tuple.Create((Stream)stream, onClose));            
        }
    }
}
