using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Files;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Files
{
    public class FileTriggerEndToEndTests
    {
        private readonly string testFileDir;
        private readonly string rootPath;
        private readonly string attributeSubPath = @"webjobs_extensionstests\import";

        public FileTriggerEndToEndTests()
        {
            rootPath = Path.GetTempPath();
            testFileDir = Path.Combine(rootPath, attributeSubPath);
            Directory.CreateDirectory(testFileDir);
            DeleteTestFiles(testFileDir);

            FileTriggerTestJobs.Processed.Clear();
        }

        [Fact]
        public async void JobIsTriggeredForNewFiles()
        {
            JobHost host = CreateTestJobHost();

            host.Start();

            Assert.Equal(0, FileTriggerTestJobs.Processed.Count);

            // now write a file to trigger the job
            string testFilePath = WriteTestFile();

            await Task.Delay(2000);

            Assert.Equal(1, FileTriggerTestJobs.Processed.Count);
            Assert.Equal(Path.GetFileName(testFilePath), FileTriggerTestJobs.Processed.Single());
            Assert.True(File.Exists(testFilePath));

            // write a non .dat file - don't expect it to trigger the job
            string ignoreFilePath = WriteTestFile("txt");

            await Task.Delay(2000);

            Assert.Equal(1, FileTriggerTestJobs.Processed.Count);
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
                // now write a file to trigger the job
                string testFilePath = WriteTestFile();
                filesToProcess.Add(Path.GetFileName(testFilePath));
            }

            // write a non .dat file - don't expect it to be processed
            WriteTestFile("txt");

            host.Start();

            await TestHelpers.Await(() =>
                {
                    return FileTriggerTestJobs.Processed.Count == preexistingFileCount;
                });

            Assert.True(FileTriggerTestJobs.Processed.OrderBy(p => p).SequenceEqual(filesToProcess.OrderBy(p => p)));

            host.Stop();
        }

        private JobHost CreateTestJobHost()
        {
            ExplicitTypeLocator locator = new ExplicitTypeLocator(typeof(FileTriggerTestJobs));
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
            string testFilePath = Path.Combine(testFileDir, testFileName);
            File.WriteAllText(testFilePath, "TestData");
            Assert.True(File.Exists(testFilePath));

            return testFilePath;
        }
    }

    public static class FileTriggerTestJobs
    {
        public static List<string> Processed = new List<string>();

        public static void ImportTestJob(
            [FileTrigger(@"webjobs_extensionstests\import\{name}", filter: "*.dat")] FileStream file,
            string name)
        {
            Processed.Add(name);
            file.Close();
        }
    }
}
