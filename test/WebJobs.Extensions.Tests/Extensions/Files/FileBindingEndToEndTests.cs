// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Files;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Files
{
    public class FileBindingEndToEndTests
    {
        private readonly string testInputDir;
        private readonly string testOutputDir;
        private readonly string rootPath;
        private const string ImportTestPath = @"webjobs_extensionstests\import";
        private const string OutputTestPath = @"webjobs_extensionstests\output";

        public FileBindingEndToEndTests()
        {
            rootPath = Path.GetTempPath();

            testInputDir = Path.Combine(rootPath, ImportTestPath);
            Directory.CreateDirectory(testInputDir);
            DeleteTestFiles(testInputDir);

            testOutputDir = Path.Combine(rootPath, OutputTestPath);
            Directory.CreateDirectory(testOutputDir);
            DeleteTestFiles(testOutputDir);

            FilesTestJobs.Processed.Clear();
        }

        [Fact]
        public async void JobIsTriggeredForNewFiles()
        {
            JobHost host = CreateTestJobHost();

            host.Start();

            Assert.Equal(0, FilesTestJobs.Processed.Count);

            // now write a file to trigger the job
            string testFilePath = WriteTestFile();

            await Task.Delay(2000);

            Assert.Equal(1, FilesTestJobs.Processed.Count);
            Assert.Equal(Path.GetFileName(testFilePath), FilesTestJobs.Processed.Single());
            Assert.True(File.Exists(testFilePath));

            // write a non .dat file - don't expect it to trigger the job
            string ignoreFilePath = WriteTestFile("txt");

            await Task.Delay(2000);

            Assert.Equal(1, FilesTestJobs.Processed.Count);
            Assert.True(File.Exists(ignoreFilePath));

            host.Stop();
        }

        [Fact]
        public async void ExistingFilesAreBatchProcessedOnStartup()
        {
            JobHost host = CreateTestJobHost();

            // create a bunch of preexisting files
            List<string> filesToProcess = new List<string>();
            int preexistingFileCount = 10;
            for (int i = 0; i < preexistingFileCount; i++)
            {
                string testFilePath = WriteTestFile();
                filesToProcess.Add(Path.GetFileName(testFilePath));
            }

            // write a non .dat file - don't expect it to be processed
            WriteTestFile("txt");

            host.Start();

            await TestHelpers.Await(() =>
                {
                    return FilesTestJobs.Processed.Count == preexistingFileCount;
                });

            Assert.True(FilesTestJobs.Processed.OrderBy(p => p).SequenceEqual(filesToProcess.OrderBy(p => p)));

            host.Stop();
        }

        [Fact]
        public async Task FileAttribute_SupportsExpectedOutputBindings()
        {
            JobHost host = CreateTestJobHost();
            host.Start();

            await VerifyOutputBinding(typeof(FilesTestJobs).GetMethod("BindToStringOutput"));
            await VerifyOutputBinding(typeof(FilesTestJobs).GetMethod("BindToByteArrayOutput"));
            await VerifyOutputBinding(typeof(FilesTestJobs).GetMethod("BindToStreamOutput"));
            await VerifyOutputBinding(typeof(FilesTestJobs).GetMethod("BindToFileStreamOutput"));
            await VerifyOutputBinding(typeof(FilesTestJobs).GetMethod("BindToStreamWriterOutput"));
            await VerifyOutputBinding(typeof(FilesTestJobs).GetMethod("BindToTextWriterOutput"));

            host.Stop();
        }

        [Fact]
        public async Task FileAttribute_SupportsExpectedInputBindings()
        {
            JobHost host = CreateTestJobHost();
            host.Start();

            await VerifyInputBinding(host, typeof(FilesTestJobs).GetMethod("BindToStringInput"));
            await VerifyInputBinding(host, typeof(FilesTestJobs).GetMethod("BindToByteArrayInput"));
            await VerifyInputBinding(host, typeof(FilesTestJobs).GetMethod("BindToStreamInput"));
            await VerifyInputBinding(host, typeof(FilesTestJobs).GetMethod("BindToStreamReaderInput"));
            await VerifyInputBinding(host, typeof(FilesTestJobs).GetMethod("BindToTextReaderInput"));
            await VerifyInputBinding(host, typeof(FilesTestJobs).GetMethod("BindToFileInfoInput"));

            host.Stop();
        }

        [Fact]
        public async Task FileBinding_SupportsNameResolver()
        {
            JobHost host = CreateTestJobHost();

            string expectedOutputFilePath = Path.Combine(rootPath, OutputTestPath, "TestValue.txt");
            File.Delete(expectedOutputFilePath);
            Assert.False(File.Exists(expectedOutputFilePath));

            var method = typeof(FilesTestJobs).GetMethod("BindUsingNameResolver");
            await host.CallAsync(method);

            Assert.True(File.Exists(expectedOutputFilePath));
        }

        private async Task VerifyInputBinding(JobHost host, MethodInfo method)
        {
            string data = Guid.NewGuid().ToString();
            string inputFile = Path.Combine(rootPath, ImportTestPath, string.Format("{0}.txt", method.Name));
            File.WriteAllText(inputFile, data);

            host.Call(method);

            string outputFile = Path.Combine(rootPath, OutputTestPath, string.Format("{0}.txt", method.Name));
            await TestHelpers.Await(() =>
            {
                return File.Exists(outputFile);
            });

            // give time for file to close
            await Task.Delay(1000);

            string result = File.ReadAllText(outputFile);
            Assert.Equal(data, result);
        }

        private async Task VerifyOutputBinding(MethodInfo method)
        {
            string data = Guid.NewGuid().ToString();
            string inputFile = Path.Combine(rootPath, ImportTestPath, string.Format("{0}.txt", method.Name));
            File.WriteAllText(inputFile, data);

            string outputFile = Path.Combine(rootPath, OutputTestPath, string.Format("{0}.txt", method.Name));
            await TestHelpers.Await(() =>
            {
                return File.Exists(outputFile);
            });

            // give time for file to close
            await Task.Delay(1000);

            string result = File.ReadAllText(outputFile);
            Assert.Equal(data, result);
        }

        private JobHost CreateTestJobHost()
        {
            ExplicitTypeLocator locator = new ExplicitTypeLocator(typeof(FilesTestJobs));
            var resolver = new TestNameResolver();
            resolver.Values.Add("test", "TestValue");
            JobHostConfiguration config = new JobHostConfiguration
            {
                TypeLocator = locator,
                NameResolver = resolver
            };

            FilesConfiguration filesConfig = new FilesConfiguration
            {
                RootPath = rootPath
            };
            config.UseFiles(filesConfig);

            return new JobHost(config);
        }

        private void DeleteTestFiles(string path)
        {
            foreach (string file in Directory.GetFiles(path))
            {
                File.Delete(file);
            }
        }

        private string WriteTestFile(string extension = "dat")
        {
            string testFileName = string.Format("{0}.{1}", Guid.NewGuid(), extension);
            string testFilePath = Path.Combine(testInputDir, testFileName);
            File.WriteAllText(testFilePath, "TestData");
            Assert.True(File.Exists(testFilePath));

            return testFilePath;
        }

        public static class FilesTestJobs
        {
            static FilesTestJobs()
            {
                Processed = new List<string>();
            }

            public static List<string> Processed { get; private set; }

            public static void ImportTestJob(
                [FileTrigger(ImportTestPath + @"/{name}", filter: "*.dat")] FileStream file,
                string name)
            {
                Processed.Add(name);
                file.Close();
            }

            [NoAutomaticTrigger]
            public static void BindUsingNameResolver(
                [File(OutputTestPath + @"\%test%.txt", FileAccess.Write)] out string output)
            {
                output = "Test";
            }

            public static void BindToStringOutput(
                [FileTrigger(ImportTestPath + @"\{name}", filter: "BindToStringOutput.txt")] string input,
                [File(OutputTestPath + @"\{name}", FileAccess.Write)] out string output)
            {
                output = input;
            }

            public static void BindToByteArrayOutput(
                [FileTrigger(ImportTestPath + @"\{name}", filter: "BindToByteArrayOutput.txt")] FileStream input,
                [File(OutputTestPath + @"\{name}", FileAccess.Write)] out byte[] output)
            {
                using (StreamReader reader = new StreamReader(input))
                {
                    string text = reader.ReadToEnd();
                    output = Encoding.UTF8.GetBytes(text);
                }
            }

            public static void BindToStreamOutput(
                [FileTrigger(ImportTestPath + @"\{name}", filter: "BindToStreamOutput.txt")] string input,
                [File(OutputTestPath + @"\{name}", FileAccess.Write)] Stream output)
            {
                using (StreamWriter sw = new StreamWriter(output))
                {
                    sw.Write(input);
                }
            }

            public static void BindToStreamWriterOutput(
                [FileTrigger(ImportTestPath + @"\{name}", filter: "BindToStreamWriterOutput.txt")] Stream input,
                [File(OutputTestPath + @"\{name}", FileAccess.Write)] StreamWriter output)
            {
                using (StreamReader reader = new StreamReader(input))
                {
                    string text = reader.ReadToEnd();
                    output.Write(text);
                } 
            }

            public static void BindToTextWriterOutput(
                [FileTrigger(ImportTestPath + @"\{name}", filter: "BindToTextWriterOutput.txt")] FileSystemEventArgs input,
                [File(OutputTestPath + @"\{name}", FileAccess.Write)] TextWriter output)
            {
                string text = File.ReadAllText(input.FullPath);
                output.Write(text);
            }

            public static void BindToFileStreamOutput(
                [FileTrigger(ImportTestPath + @"\{name}", filter: "BindToFileStreamOutput.txt")] FileInfo input,
                [File(OutputTestPath + @"\{name}", FileAccess.Write)] FileStream output)
            {
                using (FileStream stream = input.OpenRead())
                {
                    stream.CopyTo(output);
                }
            }

            public static void BindToStreamInput(
                [File(ImportTestPath + @"\BindToStreamInput.txt")] Stream input,
                [File(OutputTestPath + @"\BindToStreamInput.txt", FileAccess.Write)] Stream output)
            {
                input.CopyTo(output);
            }

            public static void BindToStreamReaderInput(
                [File(ImportTestPath + @"\BindToStreamReaderInput.txt")] StreamReader input,
                [File(OutputTestPath + @"\BindToStreamReaderInput.txt", FileAccess.Write)] Stream output)
            {
                string text = input.ReadToEnd();
                using (StreamWriter sw = new StreamWriter(output))
                {
                    sw.Write(text);
                }
            }

            public static void BindToTextReaderInput(
                [File(ImportTestPath + @"\BindToTextReaderInput.txt")] TextReader input,
                [File(OutputTestPath + @"\BindToTextReaderInput.txt", FileAccess.Write)] Stream output)
            {
                string text = input.ReadToEnd();
                using (StreamWriter sw = new StreamWriter(output))
                {
                    sw.Write(text);
                }
            }

            public static void BindToStringInput(
                [File(ImportTestPath + @"\BindToStringInput.txt")] string input,
                [File(OutputTestPath + @"\BindToStringInput.txt", FileAccess.Write)] out string output)
            {
                output = input;
            }

            public static void BindToByteArrayInput(
                [File(ImportTestPath + @"\BindToByteArrayInput.txt")] byte[] input,
                [File(OutputTestPath + @"\BindToByteArrayInput.txt", FileAccess.Write)] out byte[] output)
            {
                output = input;
            }

            public static void BindToFileInfoInput(
                [File(ImportTestPath + @"\BindToFileInfoInput.txt")] FileInfo input,
                [File(OutputTestPath + @"\BindToFileInfoInput.txt", FileAccess.Write)] Stream output)
            {
                using (FileStream stream = input.OpenRead())
                {
                    stream.CopyTo(output);
                }
            }
        }
    }
}
