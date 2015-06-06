using System;
using System.IO;
using Microsoft.Azure.WebJobs.Host.Converters;

namespace Microsoft.Azure.WebJobs.Extensions.Files.Converters
{
    internal class StringToFileSystemEventArgsConverter : IConverter<string, FileSystemEventArgs>
    {
        public FileSystemEventArgs Convert(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                throw new ArgumentNullException("input");
            }

            if (!File.Exists(input))
            {
                return null;
            }

            // TODO: This only supports Created events. For Dashboard invocation, how can we
            // handle Change events?
            string directory = Path.GetDirectoryName(input);
            string fileName = Path.GetFileName(input);

            return new FileSystemEventArgs(WatcherChangeTypes.Created, directory, fileName);
        }
    }
}
