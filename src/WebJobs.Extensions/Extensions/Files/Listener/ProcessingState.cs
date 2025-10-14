// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.Files.Listener
{
    /// <summary>
    /// Enumeration of the possible processing states a
    /// file can be in.
    /// </summary>
    internal enum ProcessingState
    {
        /// <summary>
        /// The file is being processed.
        /// </summary>
        Processing,

        /// <summary>
        /// Processing is complete for the file.
        /// </summary>
        Processed,

        /// <summary>
        /// Processing has failed for the file.
        /// </summary>
        Failed
    }
}
