using System;
using System.IO;

namespace WebJobs.Extensions.Files
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public class FileTriggerAttribute : Attribute
    {
        public FileTriggerAttribute(string path, string filter = null, WatcherChangeTypes changeTypes = WatcherChangeTypes.Created, bool autoDelete = false)
        {
            this.Path = path;
            this.Filter = filter;
            this.ChangeTypes = changeTypes;
            this.AutoDelete = autoDelete;
        }

        public string Path { get; set; }
        public string Filter { get; set; }
        public WatcherChangeTypes ChangeTypes { get; set; }
        public bool AutoDelete { get; set; }
    }
}
