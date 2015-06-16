using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Files;
using Microsoft.Azure.WebJobs.Extensions.Files.Listener;
using Microsoft.Azure.WebJobs.Host.Executors;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Files.Listener
{
    public class FileProcessorTests
    {
        private const string InstanceId = "3b151065ae0740f5c4c278989981d9090cd27d8440cdd27ee155a9f0d0ef6bb9";
        private FileProcessor processor;
        private FilesConfiguration config;
        private readonly string combinedTestFilePath;
        private readonly string rootPath;
        private Mock<ITriggeredFunctionExecutor<FileSystemEventArgs>> mockExecutor;
        private const string attributeSubPath = @"webjobs_extensionstests\import";
        private JsonSerializer _serializer;

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
            
            FileTriggerAttribute attribute = new FileTriggerAttribute(attributeSubPath, "*.dat");
            processor = CreateTestProcessor(attribute);

            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
            };
            _serializer = JsonSerializer.Create(settings);

            Environment.SetEnvironmentVariable("WEBSITE_INSTANCE_ID", InstanceId);
        }

        [Fact]
        public async Task ProcessFileAsync_AlreadyProcessing_ReturnsWithoutProcessing()
        {
            string testFile = WriteTestFile("dat");

            // first take a lock on the status file
            using (StreamWriter statusFile = processor.AquireStatusFileLock(testFile, WatcherChangeTypes.Created))
            {
                // now attempt to process the file
                FileSystemEventArgs eventArgs = new FileSystemEventArgs(WatcherChangeTypes.Created, Path.GetDirectoryName(testFile), Path.GetFileName(testFile));

                bool fileProcessedSuccessfully = await processor.ProcessFileAsync(eventArgs);

                Assert.False(fileProcessedSuccessfully);
                mockExecutor.Verify(p => p.TryExecuteAsync(It.IsAny<TriggeredFunctionData<FileSystemEventArgs>>(), It.IsAny<CancellationToken>()), Times.Never);
            }

            string statusFilePath = processor.GetStatusFile(testFile);
            File.Delete(statusFilePath);
        }

        [Fact]
        public async Task ProcessFileAsync_Failure_LeavesInProgressStatusFile()
        {
            string testFile = WriteTestFile("dat");

            FunctionResult result = new FunctionResult(false);
            mockExecutor.Setup(p => p.TryExecuteAsync(It.IsAny<TriggeredFunctionData<FileSystemEventArgs>>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);

            FileSystemEventArgs eventArgs = new FileSystemEventArgs(WatcherChangeTypes.Created, Path.GetDirectoryName(testFile), Path.GetFileName(testFile));
            bool fileProcessedSuccessfully = await processor.ProcessFileAsync(eventArgs);

            Assert.False(fileProcessedSuccessfully);
            Assert.True(File.Exists(testFile));
            string statusFilePath = processor.GetStatusFile(testFile);
            StatusFileEntry entry = processor.GetLastStatus(statusFilePath);
            Assert.Equal(ProcessingState.Processing, entry.State);
        }

        [Fact]
        public async Task ProcessFileAsync_ChangeTypeCreate_Success()
        {
            FileTriggerAttribute attribute = new FileTriggerAttribute(attributeSubPath, "*.dat");
            processor = CreateTestProcessor(attribute);

            string testFile = WriteTestFile("dat");

            FunctionResult result = new FunctionResult(true);
            mockExecutor.Setup(p => p.TryExecuteAsync(It.IsAny<TriggeredFunctionData<FileSystemEventArgs>>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);

            string testFilePath = Path.GetDirectoryName(testFile);
            string testFileName = Path.GetFileName(testFile);
            FileSystemEventArgs eventArgs = new FileSystemEventArgs(WatcherChangeTypes.Created, testFilePath, testFileName);
            bool fileProcessedSuccessfully = await processor.ProcessFileAsync(eventArgs);

            Assert.True(fileProcessedSuccessfully);
            string expectedStatusFile = processor.GetStatusFile(testFile);
            Assert.True(File.Exists(testFile));
            Assert.True(File.Exists(expectedStatusFile));
            string[] lines = File.ReadAllLines(expectedStatusFile);
            Assert.Equal(2, lines.Length);

            StatusFileEntry entry = (StatusFileEntry)_serializer.Deserialize(new StringReader(lines[0]), typeof(StatusFileEntry));
            Assert.Equal(ProcessingState.Processing, entry.State);
            Assert.Equal(WatcherChangeTypes.Created, entry.ChangeType);
            Assert.Equal(InstanceId.Substring(0, 20), entry.InstanceId);

            entry = (StatusFileEntry)_serializer.Deserialize(new StringReader(lines[1]), typeof(StatusFileEntry));
            Assert.Equal(ProcessingState.Processed, entry.State);
            Assert.Equal(WatcherChangeTypes.Created, entry.ChangeType);
            Assert.Equal(InstanceId.Substring(0, 20), entry.InstanceId);
        }

        [Fact]
        public async Task ProcessFileAsync_ChangeTypeChange_Success()
        {
            FileTriggerAttribute attribute = new FileTriggerAttribute(attributeSubPath, "*.dat");
            processor = CreateTestProcessor(attribute);

            string testFile = WriteTestFile("dat");

            FunctionResult result = new FunctionResult(true);
            mockExecutor.Setup(p => p.TryExecuteAsync(It.IsAny<TriggeredFunctionData<FileSystemEventArgs>>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);

            // first process a Create event
            string testFilePath = Path.GetDirectoryName(testFile);
            string testFileName = Path.GetFileName(testFile);
            FileSystemEventArgs eventArgs = new FileSystemEventArgs(WatcherChangeTypes.Created, testFilePath, testFileName);
            bool fileProcessedSuccessfully = await processor.ProcessFileAsync(eventArgs);
            Assert.True(fileProcessedSuccessfully);

            // now process a Change event
            File.WriteAllText(testFile, "update");
            eventArgs = new FileSystemEventArgs(WatcherChangeTypes.Changed, testFilePath, testFileName);
            fileProcessedSuccessfully = await processor.ProcessFileAsync(eventArgs);
            Assert.True(fileProcessedSuccessfully);

            string expectedStatusFile = processor.GetStatusFile(testFile);
            Assert.True(File.Exists(testFile));
            Assert.True(File.Exists(expectedStatusFile));
            string[] lines = File.ReadAllLines(expectedStatusFile);
            Assert.Equal(4, lines.Length);

            StatusFileEntry entry = (StatusFileEntry)_serializer.Deserialize(new StringReader(lines[0]), typeof(StatusFileEntry));
            Assert.Equal(ProcessingState.Processing, entry.State);
            Assert.Equal(WatcherChangeTypes.Created, entry.ChangeType);
            Assert.Equal(InstanceId.Substring(0, 20), entry.InstanceId);

            entry = (StatusFileEntry)_serializer.Deserialize(new StringReader(lines[1]), typeof(StatusFileEntry));
            Assert.Equal(ProcessingState.Processed, entry.State);
            Assert.Equal(WatcherChangeTypes.Created, entry.ChangeType);
            Assert.Equal(InstanceId.Substring(0, 20), entry.InstanceId);

            entry = (StatusFileEntry)_serializer.Deserialize(new StringReader(lines[2]), typeof(StatusFileEntry));
            Assert.Equal(ProcessingState.Processing, entry.State);
            Assert.Equal(WatcherChangeTypes.Changed, entry.ChangeType);
            Assert.Equal(InstanceId.Substring(0, 20), entry.InstanceId);

            entry = (StatusFileEntry)_serializer.Deserialize(new StringReader(lines[3]), typeof(StatusFileEntry));
            Assert.Equal(ProcessingState.Processed, entry.State);
            Assert.Equal(WatcherChangeTypes.Changed, entry.ChangeType);
            Assert.Equal(InstanceId.Substring(0, 20), entry.InstanceId);
        }

        [Fact]
        public async Task ProcessFileAsync_JobFunctionSucceeds()
        {
            string testFile = WriteTestFile("dat");

            FunctionResult result = new FunctionResult(true);
            mockExecutor.Setup(p => p.TryExecuteAsync(It.IsAny<TriggeredFunctionData<FileSystemEventArgs>>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);

            FileSystemEventArgs eventArgs = new FileSystemEventArgs(WatcherChangeTypes.Created, combinedTestFilePath, Path.GetFileName(testFile));
            await processor.ProcessFileAsync(eventArgs);

            Assert.Equal(2, Directory.GetFiles(combinedTestFilePath).Length);
            string expectedStatusFile = processor.GetStatusFile(testFile);
            Assert.True(File.Exists(testFile));
            Assert.True(File.Exists(expectedStatusFile));

            string[] statusLines = File.ReadAllLines(expectedStatusFile);
            Assert.Equal(2, statusLines.Length);
            StatusFileEntry entry = (StatusFileEntry)_serializer.Deserialize(new StringReader(statusLines[0]), typeof(StatusFileEntry));
            Assert.Equal(ProcessingState.Processing, entry.State);
            Assert.Equal(WatcherChangeTypes.Created, entry.ChangeType);
            Assert.Equal(processor.InstanceId, entry.InstanceId);
            entry = (StatusFileEntry)_serializer.Deserialize(new StringReader(statusLines[1]), typeof(StatusFileEntry));
            Assert.Equal(ProcessingState.Processed, entry.State);
            Assert.Equal(WatcherChangeTypes.Created, entry.ChangeType);
            Assert.Equal(processor.InstanceId, entry.InstanceId);
        }

        [Fact]
        public void Cleanup_AutoDeleteOn_DeletesCompletedFiles()
        {
            FileTriggerAttribute attribute = new FileTriggerAttribute(attributeSubPath, "*.dat", autoDelete: true);
            FileProcessor localProcessor = CreateTestProcessor(attribute);

            // create a completed file set
            string completedFile = WriteTestFile("dat");
            string completedStatusFile = localProcessor.GetStatusFile(completedFile);
            StatusFileEntry status = new StatusFileEntry
            {
                State = ProcessingState.Processing,
                Timestamp = DateTime.UtcNow,
                ChangeType = WatcherChangeTypes.Created,
                InstanceId = "1"
            };
            StringWriter sw = new StringWriter();
            _serializer.Serialize(sw, status);
            sw.WriteLine();
            status.State = ProcessingState.Processed;
            status.Timestamp = status.Timestamp + TimeSpan.FromSeconds(15);
            _serializer.Serialize(sw, status);
            sw.WriteLine();
            sw.Flush();
            File.WriteAllText(completedStatusFile, sw.ToString());

            // include an additional companion metadata file
            string completedAdditionalFile = completedFile + ".metadata";
            File.WriteAllText(completedAdditionalFile, "Data");

            // write a file that SHOULDN'T be deleted
            string dontDeleteFile = Path.ChangeExtension(completedFile, "json");
            File.WriteAllText(dontDeleteFile, "Data");

            // create an incomplete file set
            string incompleteFile = WriteTestFile("dat");
            string incompleteStatusFile = localProcessor.GetStatusFile(incompleteFile);
            status = new StatusFileEntry
            {
                State = ProcessingState.Processing,
                Timestamp = DateTime.UtcNow,
                ChangeType = WatcherChangeTypes.Created,
                InstanceId = "1"
            };
            sw = new StringWriter();
            _serializer.Serialize(sw, status);
            sw.WriteLine();
            File.WriteAllText(incompleteStatusFile, sw.ToString());

            localProcessor.Cleanup();

            // expect the completed set to be deleted
            Assert.False(File.Exists(completedFile));
            Assert.False(File.Exists(completedAdditionalFile));
            Assert.False(File.Exists(completedStatusFile));
            Assert.True(File.Exists(dontDeleteFile));

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
