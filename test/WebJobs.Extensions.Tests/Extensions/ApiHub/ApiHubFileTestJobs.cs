// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.ApiHub
{
    public static class ApiHubFileTestJobs
    {
        static ApiHubFileTestJobs()
        {
            Processed = new List<string>();
        }

        public static List<string> Processed { get; private set; }

        public static void ImportTestJob(
            [ApiHubFileTrigger("dropbox", ApiHubTestFixture.ImportTestPath + @"/{name}")] string input,
            string name)
        {
            Processed.Add(name);
        }

        public static void PathsTestJob1(
            [ApiHubFileTrigger("dropbox", ApiHubTestFixture.PathsTestPath + @"/{name}")] string input,
            [ApiHubFile("dropbox", ApiHubTestFixture.OutputTestPath + @"/{name}.path1", FileAccess.Write)] out string outputString)
        {
            outputString = input;            
        }

        public static void PathsTestJob2(
            [ApiHubFileTrigger("dropbox", "/" + ApiHubTestFixture.PathsTestPath + @"/{name}")] string input,
            [ApiHubFile("dropbox", "/" + ApiHubTestFixture.OutputTestPath + @"/{name}.path2", FileAccess.Write)] out string outputString)
        {
            outputString = input;
        }

        public static void ThrowException(
            [ApiHubFileTrigger("dropbox", ApiHubTestFixture.ExceptionPath + @"/{name}")] Stream sr,
            string name)
        {
            throw new ApplicationException("Error");
        }

        public static void BindToOutputTypes(
            [ApiHubFileTrigger("dropbox", ApiHubTestFixture.ImportTestPath + @"/{name}")] string input,
            [ApiHubFile("dropbox", ApiHubTestFixture.OutputTestPath + @"/{name}.string", FileAccess.Write)] out string outputString,
            [ApiHubFile("dropbox", ApiHubTestFixture.OutputTestPath + @"/{name}.byte", FileAccess.Write)] out byte[] outputByte,
            [ApiHubFile("dropbox", ApiHubTestFixture.OutputTestPath + @"/{name}.stream", FileAccess.Write)] Stream outputStream,
            [ApiHubFile("dropbox", ApiHubTestFixture.OutputTestPath + @"/{name}.streamWriter", FileAccess.Write)] StreamWriter outputStreamWriter,
            [ApiHubFile("dropbox", ApiHubTestFixture.OutputTestPath + @"/{name}.textWriter", FileAccess.Write)] TextWriter outputTextWriter)
        {
            outputString = input;
            outputByte = Encoding.UTF8.GetBytes(input);

            StreamWriter sw = new StreamWriter(outputStream);
            sw.Write(input);
            sw.Flush();

            using (StringReader reader = new StringReader(input))
            {
                string text = reader.ReadToEnd();
                outputStreamWriter.Write(text);
            }

            outputTextWriter.Write(input);
        }

        public static void BindToStreamInput(
            [ApiHubFile("dropbox", ApiHubTestFixture.ImportTestPath + @"/BindToStreamInput.txt")] Stream input,
            [ApiHubFile("dropbox", ApiHubTestFixture.OutputTestPath + @"/BindToStreamInput.txt", FileAccess.Write)] Stream output)
        {
            input.CopyTo(output);
        }

        public static void BindToStreamReaderInput(
            [ApiHubFile("dropbox", ApiHubTestFixture.ImportTestPath + @"/BindToStreamReaderInput.txt")] StreamReader input,
            [ApiHubFile("dropbox", ApiHubTestFixture.OutputTestPath + @"/BindToStreamReaderInput.txt", FileAccess.Write)] out string output)
        {
            output = input.ReadToEnd();
        }

        public static void BindToTextReaderInput(
            [ApiHubFile("dropbox", ApiHubTestFixture.ImportTestPath + @"/BindToTextReaderInput.txt")] TextReader input,
            [ApiHubFile("dropbox", ApiHubTestFixture.OutputTestPath + @"/BindToTextReaderInput.txt", FileAccess.Write)] out string output)
        {
            output = input.ReadToEnd();
        }

        public static void BindToStringInput(
            [ApiHubFile("dropbox", ApiHubTestFixture.ImportTestPath + "/BindToStringInput.txt")] string input,
            [ApiHubFile("dropbox", ApiHubTestFixture.OutputTestPath + "/BindToStringInput.txt", FileAccess.Write)] out string output)
        {
            output = input;
        }

        public static void BindToByteArrayInput(
            [ApiHubFile("dropbox", ApiHubTestFixture.ImportTestPath + @"/BindToByteArrayInput.txt")] byte[] input,
            [ApiHubFile("dropbox", ApiHubTestFixture.OutputTestPath + @"/BindToByteArrayInput.txt", FileAccess.Write)] out string output)
        {
            output = Encoding.UTF8.GetString(input);
        }

        [NoAutomaticTrigger]
        public static void ImperativeBind(
            Binder binder)
        {
            var fileName = ApiHubTestFixture.OutputTestPath + "/ImperativeBind.txt";

            var writer = binder.Bind<TextWriter>(new ApiHubFileAttribute("dropbox2", fileName, FileAccess.Write));
            var content = "Generated by Azure Functions";

            writer.Write(content);
            writer.Flush();
        }

    }
}
