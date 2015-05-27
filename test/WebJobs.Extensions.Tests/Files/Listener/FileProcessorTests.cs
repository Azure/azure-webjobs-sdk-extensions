using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Moq;
using WebJobs.Extensions.Files;
using WebJobs.Extensions.Files.Listener;
using Xunit;

namespace WebJobs.Extensions.Tests.Files.Listener
{
    public class FileProcessorTests
    {
        private FileProcessor processor;
        private FilesConfiguration config;
        private readonly string combinedTestFilePath;
        private readonly string rootPath;
        private Mock<ITriggeredFunctionExecutor<FileSystemEventArgs>> mockExecutor;
        private const string attributeSubPath = @"webjobs_extensionstests\import";

        public FileProcessorTests()
        {
            rootPath = Path.GetTempPath();
            combinedTestFilePath = Path.Combine(rootPath, attributeSubPath);
            Directory.CreateDirectory(combinedTestFilePath);
            DeleteTestFiles(combinedTestFilePath);

            config = new FilesConfiguration()
            {
                RootPath = rootPath
            };
            mockExecutor = new Mock<ITriggeredFunctionExecutor<FileSystemEventArgs>>(MockBehavior.Strict);

            FileTriggerAttribute attribute = new FileTriggerAttribute(attributeSubPath, "*.dat");
            processor = CreateTestProcessor(attribute);

            Environment.SetEnvironmentVariable("WEBSITE_INSTANCE_ID", "3b151065ae0740f5c4c278989981d9090cd27d8440cdd27ee155a9f0d0ef6bb9");
        }

        [Fact]
        public void BeginProcessing_CreatesStatusFile()
        {
            string testFile = WriteTestFile("dat");

            string expectedStatusFile = Path.ChangeExtension(testFile, processor.StatusFileExtension);
            Assert.False(File.Exists(expectedStatusFile));

            bool shouldProcess = processor.BeginProcessing(testFile);
            Assert.True(shouldProcess);

            Assert.True(File.Exists(expectedStatusFile));
            string[] lines = File.ReadAllLines(expectedStatusFile);
            Assert.Equal(1, lines.Length);
            Assert.True(lines[0].StartsWith("Processing"));
        }

        [Fact]
        public void BeginProcessing_AlreadyProcessing_ReturnsFalse()
        {
            string testFile = WriteTestFile("dat");

            processor.BeginProcessing(testFile);

            bool shouldProcess = processor.BeginProcessing(testFile);
            Assert.False(shouldProcess);
        }

        [Fact]
        public void CompleteProcessing_Failure_DeletesStatusFile()
        {
            string testFile = WriteTestFile("dat");

            processor.BeginProcessing(testFile);

            string expectedStatusFile = Path.ChangeExtension(testFile, processor.StatusFileExtension);
            Assert.True(File.Exists(expectedStatusFile));

            processor.CompleteProcessing(testFile, false);

            Assert.True(File.Exists(testFile));
            Assert.False(File.Exists(expectedStatusFile));
        }

        [Fact]
        public void CompleteProcessing_Success_UpdatesStatusFile()
        {
            FileTriggerAttribute attribute = new FileTriggerAttribute(attributeSubPath, "*.dat");
            processor = CreateTestProcessor(attribute);

            string testFile = WriteTestFile("dat");

            processor.BeginProcessing(testFile);
            processor.CompleteProcessing(testFile, true);

            string expectedStatusFile = Path.ChangeExtension(testFile, processor.StatusFileExtension);
            Assert.True(File.Exists(testFile));
            Assert.True(File.Exists(expectedStatusFile));
            string[] lines = File.ReadAllLines(expectedStatusFile);
            Assert.Equal(2, lines.Length);
            Assert.True(lines[0].StartsWith("Processing"));
            Assert.True(lines[0].Contains("(Instance: 3b151065ae)"));
            Assert.True(lines[1].StartsWith("Processed"));
            Assert.True(lines[1].Contains("(Instance: 3b151065ae)"));
        }

        [Fact]
        public async Task ProcessFileAsync_JobFunctionSucceeds()
        {
            string testFile = WriteTestFile("dat");

            mockExecutor.Setup(p => p.TryExecuteAsync(It.IsAny<TriggeredFunctionData<FileSystemEventArgs>>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

            FileSystemEventArgs eventArgs = new FileSystemEventArgs(WatcherChangeTypes.Created, combinedTestFilePath, Path.GetFileName(testFile));
            await processor.ProcessFileAsync(eventArgs);

            Assert.Equal(2, Directory.GetFiles(combinedTestFilePath).Length);
            string expectedStatusFile = Path.ChangeExtension(testFile, processor.StatusFileExtension);
            Assert.True(File.Exists(testFile));
            Assert.True(File.Exists(expectedStatusFile));

            string[] lines = File.ReadAllLines(expectedStatusFile);
            Assert.Equal(2, lines.Length);
            Assert.True(lines[0].StartsWith("Processing"));
            Assert.True(lines[1].StartsWith("Processed"));
        }

        [Fact]
        public async Task ProcessFileAsync_JobFunctionFails()
        {
            string testFile = WriteTestFile("dat");

            mockExecutor.Setup(p => p.TryExecuteAsync(It.IsAny<TriggeredFunctionData<FileSystemEventArgs>>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

            FileSystemEventArgs eventArgs = new FileSystemEventArgs(WatcherChangeTypes.Created, combinedTestFilePath, Path.GetFileName(testFile));
            await processor.ProcessFileAsync(eventArgs);

            // expect the status file to be removed
            Assert.True(File.Exists(testFile));
            Assert.Equal(1, Directory.GetFiles(combinedTestFilePath).Length);
        }

        [Fact]
        public void Cleanup_AutoDeleteOn_DeletesCompletedFiles()
        {
            // create a completed file set
            string completedFile = WriteTestFile("dat");
            string completedStatusFile = Path.ChangeExtension(completedFile, "status");
            File.WriteAllLines(completedStatusFile, new string[] { "Processing", "Processed" });

            // create an incomplete file set
            string incompleteFile = WriteTestFile("dat");
            string incompleteStatusFile = Path.ChangeExtension(incompleteFile, "status");
            File.WriteAllLines(incompleteStatusFile, new string[] { "Processing" });

            FileTriggerAttribute attribute = new FileTriggerAttribute(attributeSubPath, "*.dat", autoDelete: true);
            FileProcessor localProcessor = CreateTestProcessor(attribute);

            localProcessor.Cleanup();

            // expect the completed set to be deleted
            Assert.False(File.Exists(completedFile));
            Assert.False(File.Exists(completedStatusFile));

            // expect the incomplete set to remain
            Assert.False(File.Exists(completedFile));
            Assert.False(File.Exists(completedStatusFile));
        }

        private string WriteTestFile(string extension = "dat", string fileContents = "TestData")
        {
            string testFileName = string.Format("{0}.{1}", Guid.NewGuid(), extension);
            string testFilePath = Path.Combine(combinedTestFilePath, testFileName);
            File.WriteAllText(testFilePath, fileContents);
            Assert.True(File.Exists(testFilePath));

            return testFilePath;
        }

        private void DeleteTestFiles(string path)
        {
            foreach (string file in Directory.GetFiles(path))
            {
                File.Delete(file);
            }
        }

        private FileProcessor CreateTestProcessor(FileTriggerAttribute attribute)
        {
            mockExecutor = new Mock<ITriggeredFunctionExecutor<FileSystemEventArgs>>(MockBehavior.Strict);
            FileProcessorFactoryContext context = new FileProcessorFactoryContext(config, attribute, mockExecutor.Object);
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

            return new FileProcessor(context, cancellationTokenSource);
        }
    }
}
