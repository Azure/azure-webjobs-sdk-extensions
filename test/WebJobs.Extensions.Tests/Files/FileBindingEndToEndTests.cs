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
        public async Task FileAttribute_SupportsExpectedBindings()
        {
            JobHost host = CreateTestJobHost();
            host.Start();

            await VerifyAttributeBinding(typeof(FilesTestJobs).GetMethod("BindToStringOutput"));
            await VerifyAttributeBinding(typeof(FilesTestJobs).GetMethod("BindToByteArrayOutput"));
            await VerifyAttributeBinding(typeof(FilesTestJobs).GetMethod("BindToStreamOutput"));
            await VerifyAttributeBinding(typeof(FilesTestJobs).GetMethod("BindToFileStreamOutput"));

            host.Stop();
        }

        private async Task VerifyAttributeBinding(MethodInfo method)
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
            JobHostConfiguration config = new JobHostConfiguration
            {
                TypeLocator = locator
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
            public static List<string> Processed = new List<string>();

            public static void ImportTestJob(
                [FileTrigger(ImportTestPath + @"\{name}", filter: "*.dat")] FileStream file,
                string name)
            {
                Processed.Add(name);
                file.Close();
            }

            public static void BindToStringOutput(
                [FileTrigger(ImportTestPath + @"\{name}", filter: "BindToStringOutput.txt")] string input,
                [File(OutputTestPath + @"\{name}")] out string output)
            {
                output = input;
            }

            public static void BindToByteArrayOutput(
                [FileTrigger(ImportTestPath + @"\{name}", filter: "BindToByteArrayOutput.txt")] string input,
                [File(OutputTestPath + @"\{name}")] out byte[] output)
            {
                output = Encoding.UTF8.GetBytes(input);
            }

            public static void BindToStreamOutput(
                [FileTrigger(ImportTestPath + @"\{name}", filter: "BindToStreamOutput.txt")] string input,
                [File(OutputTestPath + @"\{name}")] Stream output)
            {
                using (StreamWriter sw = new StreamWriter(output))
                {
                    sw.Write(input);
                }
            }

            public static void BindToFileStreamOutput(
                [FileTrigger(ImportTestPath + @"\{name}", filter: "BindToFileStreamOutput.txt")] string input,
                [File(OutputTestPath + @"\{name}")] FileStream output)
            {
                using (StreamWriter sw = new StreamWriter(output))
                {
                    sw.Write(input);
                }
            }
        }
    }
}
