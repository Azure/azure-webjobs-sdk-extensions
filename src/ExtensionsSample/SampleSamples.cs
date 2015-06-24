using System;
using System.IO;
using Sample.Extension;

namespace ExtensionsSample
{
    public static class SampleSamples
    {
        public static void Sample_BindToStream([Sample(@"sample\path")] Stream stream)
        {
            using (StreamWriter sw = new StreamWriter(stream))
            {
                sw.Write("Sample");
            }
        }

        public static void Sample_BindToString([Sample(@"sample\path")] out string data)
        {
            data = "Sample";
        }

        public static void SampleTrigger([SampleTrigger(@"sample\path")] SampleTriggerValue value)
        {
            Console.WriteLine("Sample trigger job called!");
        }

        public static void SampleTrigger_BindToString([SampleTrigger(@"sample\path")] string value)
        {
            Console.WriteLine("Sample trigger job called!");
        }
    }
}
