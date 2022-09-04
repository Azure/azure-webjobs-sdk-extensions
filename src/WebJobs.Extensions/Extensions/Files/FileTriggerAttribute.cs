// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.Azure.WebJobs.Description;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Attribute used to mark a job function that should be invoked based
    /// on file events.
    /// </summary>
    /// <remarks>
    /// The method parameter type can be one of the following:
    /// <list type="bullet">
    /// <item><description><see cref="FileStream"/></description></item>
    /// <item><description><see cref="FileInfo"/></description></item>
    /// <item><description><see cref="FileSystemEventArgs"/></description></item>
    /// <item><description><see cref="string"/></description></item>
    /// <item><description><see cref="T:byte[]"/></description></item>
    /// <item><description><see cref="System.IO.Stream"/></description></item>
    /// <item><description><see cref="System.IO.TextReader"/></description></item>
    /// <item><description><see cref="System.IO.StreamReader"/></description></item>
    /// <item><description>A user-defined type (serialized as JSON)</description></item>
    /// </list>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Parameter)]
    [Binding]
    public sealed class FileTriggerAttribute : Attribute
    {
        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="path">The root path that this trigger is configured to watch for files on.</param>
        /// <param name="filter">The optional file filter that will be used.</param>
        /// <param name="changeTypes">The <see cref="WatcherChangeTypes"/> that will be used by the file watcher. The Default is Created.</param>
        /// <param name="autoDelete">True if processed files should be deleted automatically, false otherwise. The default is False.</param>
        public FileTriggerAttribute(string path, string filter, WatcherChangeTypes changeTypes = WatcherChangeTypes.Created, bool autoDelete = false)
        {
            if (!string.IsNullOrEmpty(path))
            {
                // normalize the path (allowing the user to use either
                // "/" or "\" as a separator)
                path = path.Replace("/", "\\");
            }
            this.Path = path;
            this.Filter = filter;
            this.ChangeTypes = changeTypes;
            this.AutoDelete = autoDelete;
        }

        /// <summary>
        /// Gets the root path that this trigger is configured to watch for files on.
        /// </summary>
        public string Path { get; private set; }

        /// <summary>
        /// Gets the optional file filter that will be used.
        /// </summary>
        public string Filter { get; private set; }

        /// <summary>
        /// Gets the <see cref="WatcherChangeTypes"/> that will be used by the file watcher.
        /// </summary>
        public WatcherChangeTypes ChangeTypes { get; private set; }

        /// <summary>
        /// Gets a value indicating whether files should be automatically deleted after they
        /// are successfully processed. When set to true, all files including any companion files
        /// starting with the target file name will be deleted when the file is successfully processed.
        /// </summary>
        public bool AutoDelete { get; private set; }

        /// <summary>
        /// Returns the trigger path, minus any trailing template pattern for file name.
        /// </summary>
        /// <returns></returns>
        internal string GetRootPath()
        {
            string path = Path.TrimEnd(System.IO.Path.DirectorySeparatorChar);
            int idx = path.LastIndexOf(System.IO.Path.DirectorySeparatorChar);
            if (idx > 0 && path.IndexOfAny(new char[] { '{', '}' }, idx) > 0)
            {
                return System.IO.Path.GetDirectoryName(path);
            }
            return Path;
        }
    }
}
