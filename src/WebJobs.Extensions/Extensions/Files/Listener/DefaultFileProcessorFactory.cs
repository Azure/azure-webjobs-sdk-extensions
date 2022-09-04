// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.Files
{
    internal class DefaultFileProcessorFactory : IFileProcessorFactory
    {
        public FileProcessor CreateFileProcessor(FileProcessorFactoryContext context)
        {
            return new FileProcessor(context);
        }
    }
}
