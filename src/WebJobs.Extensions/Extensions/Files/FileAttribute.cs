// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.Azure.WebJobs.Description;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Attribute used to bind a parameter to a file.
    /// </summary>
    /// <remarks>
    /// The method parameter type can be one of the following:
    /// <list type="bullet">
    /// <item><description><see cref="FileStream"/></description></item>
    /// <item><description><see cref="FileInfo"/></description></item>
    /// <item><description><see cref="string"/></description></item>
    /// <item><description><see cref="T:byte[]"/></description></item>
    /// <item><description><see cref="System.IO.Stream"/></description></item>
    /// <item><description><see cref="System.IO.TextWriter"/></description></item>
    /// <item><description><see cref="System.IO.StreamWriter"/></description></item>
    /// <item><description><see cref="System.IO.TextReader"/></description></item>
    /// <item><description><see cref="System.IO.StreamReader"/></description></item>
    /// <item><description>A user-defined type (serialized as JSON)</description></item>
    /// </list>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.ReturnValue)]
    [Binding]
    public sealed class FileAttribute : Attribute
    {
        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="path">The file path to bind to.</param>
        /// <param name="access">The <see cref="FileAccess"/> to use.</param>
        public FileAttribute(string path, FileAccess access = FileAccess.Read)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException("path");
            }

            Path = path;
            Access = access;

            // default the file mode based on access specified
            switch (access)
            {
                case FileAccess.Read:
                    Mode = FileMode.Open;
                    break;
                case FileAccess.ReadWrite:
                case FileAccess.Write:
                    Mode = FileMode.OpenOrCreate;
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="path">The file path to bind to.</param>
        /// <param name="access">The <see cref="FileAccess"/> to use.</param>
        /// <param name="mode">The <see cref="FileMode"/> to use.</param>
        public FileAttribute(string path, FileAccess access, FileMode mode)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException("path");
            }

            Path = path;
            Access = access;
            Mode = mode;
        }

        /// <summary>
        /// Gets the file path.
        /// </summary>
        [AutoResolve]
        public string Path { get; private set; }

        /// <summary>
        /// Gets he <see cref="FileAccess"/> to use.
        /// </summary>
        public FileAccess Access { get; private set; }

        /// <summary>
        /// Gets the <see cref="FileMode"/> to use.
        /// </summary>
        public FileMode Mode { get; private set; }
    }
}
