using System;
using System.IO;

namespace Microsoft.Azure.WebJobs.Extensions.Files
{
    /// <summary>
    /// Trigger for invoking jobs based on file events.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
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
        /// Gets the auto delete setting. When set to true, all files including any companion files
        /// starting with the target file name will be deleted when the file is fully processed.
        /// </summary>
        public bool AutoDelete { get; private set; }

        /// <summary>
        /// Returns the trigger path, minus any trailing template pattern for file name
        /// </summary>
        /// <returns></returns>
        internal string GetNormalizedPath()
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
