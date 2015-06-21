using System;
using System.IO;
using Microsoft.Azure.WebJobs.Host.Converters;

namespace Microsoft.Azure.WebJobs.Extensions.Files.Converters
{
    internal class StringToFileInfoConverter : IConverter<string, FileInfo>
    {
        public FileInfo Convert(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                throw new ArgumentNullException("input");
            }

            return new FileInfo(input);
        }
    }
}
