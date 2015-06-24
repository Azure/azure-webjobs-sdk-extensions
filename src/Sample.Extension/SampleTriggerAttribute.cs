using System;

namespace Sample.Extension
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class SampleTriggerAttribute : Attribute
    {
        public SampleTriggerAttribute(string path)
        {
            Path = path;
        }

        // TODO: Define your domain specific values here
        public string Path { get; private set; }
    }
}
