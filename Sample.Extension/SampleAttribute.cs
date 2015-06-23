using System;

namespace Sample.Extension
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class SampleAttribute : Attribute
    {
        public SampleAttribute(string path)
        {
            Path = path;
        }

        // TODO: Define your domain specific values here
        public string Path { get; private set; }
    }
}
