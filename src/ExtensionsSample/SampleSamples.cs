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
    }
}
