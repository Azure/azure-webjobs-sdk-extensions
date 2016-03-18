using Microsoft.Azure.ApiHub;
using Microsoft.Azure.WebJobs.Extensions.ApiHub.Common;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.ApiHub
{
    class ApiHubFile : IFileStreamProvider
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

        public async Task<Tuple<Stream, Func<Task>>> OpenWriteStreamAsync()
        {
            MemoryStream stream = new MemoryStream();
            
            Func<Task> onClose = async () =>
            {
                stream.Position = 0;
                var bytes = stream.ToArray();
                await _fileSource.WriteAsync(bytes);
            };

            return Tuple.Create((Stream)stream, onClose);            
        }
    }
}
