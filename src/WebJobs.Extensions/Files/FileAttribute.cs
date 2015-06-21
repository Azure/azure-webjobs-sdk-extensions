using System;
using System.IO;

namespace Microsoft.Azure.WebJobs.Extensions.Files
{
    /// <summary>
    /// Binds a parameter to a file.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class FileAttribute : Attribute
    {
        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="path">The file path to bind to.</param>
        /// <param name="access">The <see cref="FileAccess"/> to use.</param>
        /// <param name="mode">The <see cref="FileMode"/> to use.</param>
        public FileAttribute(string path, FileAccess access = FileAccess.ReadWrite, FileMode mode = FileMode.Create)
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
