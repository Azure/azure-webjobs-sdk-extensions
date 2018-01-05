// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.ApiHub.Common
{
    /// <summary>
    /// Interface that defines methods needed to work with FileStream
    /// </summary>
    internal interface IFileStreamProvider
    {
        /// <summary>
        /// Get a stream that reads from the source.
        /// </summary>
        /// <returns></returns>
        Task<Stream> OpenReadStreamAsync();

        /// <summary>
        /// Stream and a "Completion" function to be called when finished writing to the stream.
        /// That can be used to flush results.
        /// Technically, the a derived stream could override Close() to call OnComplete, but that's 
        /// hard for callers to implement.
        /// </summary>
        /// <returns></returns>
        Task<Tuple<Stream, Func<object, Task>>> OpenWriteStreamAsync();
    }
}