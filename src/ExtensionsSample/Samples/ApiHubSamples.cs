using Microsoft.Azure.WebJobs.Extensions.ApiHub;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtensionsSample.Samples
{
    public static class ApiHubSamples
    {
        public static void Trigger(
    [ApiHubFileTrigger("dropbox", "cdpFiles/{name}")] string value,
    [ApiHubFile("dropbox", "test1/{name}")] TextReader tr 
    //[ApiHubFile("dropbox", "testout/{name}")] out string output
    )
        {
            //string input = tr.ReadToEnd();
            //output = "Via Webjobs:" + input;
        }

        public static void Writer(
                [ApiHubFile("dropbox", "test/file1.txt")] TextReader tr,
                [ApiHubFile("dropbox", "test/file1-out.txt")] out string output 
                )
        {
            string input = tr.ReadToEnd();
            output = "Via Webjobs:" + input;
        }
    }
}
