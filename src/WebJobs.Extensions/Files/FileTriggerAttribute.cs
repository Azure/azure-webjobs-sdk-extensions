using System;
using System.IO;

namespace WebJobs.Extensions.Files
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class FileTriggerAttribute : Attribute
    {
        public FileTriggerAttribute(string path, string filter = null, WatcherChangeTypes changeTypes = WatcherChangeTypes.Created, bool autoDelete = false)
        {
            this.Path = path;
            this.Filter = filter;
            this.ChangeTypes = changeTypes;
            this.AutoDelete = autoDelete;
        }

        public string Path { get; private set; }
        public string Filter { get; private set; }
        public WatcherChangeTypes ChangeTypes { get; private set; }
        public bool AutoDelete { get; private set; }
    }
}
