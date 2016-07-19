// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.ApiHub;

namespace Microsoft.Azure.WebJobs.Extensions.ApiHub.Common
{
    internal class ApiHubFile : IFileStreamProvider
    {
        private readonly IFileItem _fileSource;

        public ApiHubFile(IFileItem fileSource)
        {
            _fileSource = fileSource;
        }

        public string Path
        {
            get
            {
                return _fileSource.Path;
            }
        }

        public async Task<Stream> OpenReadStreamAsync()
        {
            var bytes = await _fileSource.ReadAsync();

            MemoryStream ms = new MemoryStream(bytes);
            
            return ms;            
        }

        public Task<Tuple<Stream, Func<object, Task>>> OpenWriteStreamAsync()
        {
            var stream = new MemoryStream();
            
            Func<object, Task> onClose = async obj =>
            {
                byte[] bytes = obj as byte[];
                if (bytes == null)
                {
                    stream.Position = 0;
                    bytes = stream.ToArray();
                }

                await _fileSource.WriteAsync(bytes);
            };

            return Task.FromResult(Tuple.Create((Stream)stream, onClose));            
        }

        internal static ApiHubFile New(IFolderItem rootFolder, string path)
        {
            var fileSource = rootFolder.GetFileReference(path, true);
            return new ApiHubFile(fileSource);
        }
    }
}
