﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Files;
using Microsoft.Azure.WebJobs.Extensions.Files.Listener;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Files.Listeners;
using Microsoft.Azure.WebJobs.Host.Executors;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Files.Listener
{
    public class FileListenerTests
    {
        private readonly string testFileDir;
        private readonly string rootPath;
        private readonly string attributeSubPath = @"webjobs_extensionstests\import";

        public FileListenerTests()
        {
            rootPath = Path.GetTempPath();
            testFileDir = Path.Combine(rootPath, attributeSubPath);
            Directory.CreateDirectory(testFileDir);
            DeleteTestFiles(testFileDir);
        }

        [Fact]
        public void Constructor_ThrowsOnInvalidRootPath()
        {
            FilesConfiguration config = new FilesConfiguration();
            FileTriggerAttribute attrib = new FileTriggerAttribute("test", "*.dat");
            Mock<ITriggeredFunctionExecutor> mockExecutor = new Mock<ITriggeredFunctionExecutor>();

            var ex = Assert.Throws<InvalidOperationException>(() =>
            {
                new FileListener(config, attrib, mockExecutor.Object, new TestTraceWriter());
            });

            Assert.Equal("Path '' is invalid. FilesConfiguration.RootPath must be set to a valid directory location.", ex.Message);
        }

        [Theory]
        [InlineData(1, 100)]
        [InlineData(4, 200)]
        [InlineData(5, 400)]
        public async Task ConcurrentListeners_ProcessFilesCorrectly(int concurrentListenerCount, int inputFileCount)
        {
            // mock out the executor so we can capture function invocations
            Mock<ITriggeredFunctionExecutor> mockExecutor = new Mock<ITriggeredFunctionExecutor>(MockBehavior.Strict);
            ConcurrentBag<string> processedFiles = new ConcurrentBag<string>();
            FunctionResult result = new FunctionResult(true);
            mockExecutor.Setup(p => p.TryExecuteAsync(It.IsAny<TriggeredFunctionData>(), It.IsAny<CancellationToken>()))
                .Callback<TriggeredFunctionData, CancellationToken>(async (mockData, mockToken) =>
                    {
                        await Task.Delay(50);
                        FileSystemEventArgs fileEvent = mockData.TriggerValue as FileSystemEventArgs;
                        processedFiles.Add(fileEvent.Name);
                    })
                .ReturnsAsync(result);

            FilesConfiguration config = new FilesConfiguration()
            {
                RootPath = rootPath
            };
            FileTriggerAttribute attribute = new FileTriggerAttribute(attributeSubPath, changeTypes: WatcherChangeTypes.Created | WatcherChangeTypes.Changed, filter: "*.dat");

            // create a bunch of listeners and start them
            CancellationTokenSource tokenSource = new CancellationTokenSource();
            CancellationToken cancellationToken = tokenSource.Token;
            List<Task> listenerStartupTasks = new List<Task>();
            List<FileListener> listeners = new List<FileListener>();
            for (int i = 0; i < concurrentListenerCount; i++)
            {
                FileListener listener = new FileListener(config, attribute, mockExecutor.Object, new TestTraceWriter());
                listeners.Add(listener);
                listenerStartupTasks.Add(listener.StartAsync(cancellationToken));
            }
            await Task.WhenAll(listenerStartupTasks);

            // now start creating files
            List<string> expectedFiles = new List<string>();
            for (int i = 0; i < inputFileCount; i++)
            {
                string file = WriteTestFile();
                await Task.Delay(50);
                expectedFiles.Add(Path.GetFileName(file));
            }

            // wait for all files to be processed
            await TestHelpers.Await(() =>
            {
                return processedFiles.Count >= inputFileCount;
            });
            Assert.Equal(inputFileCount, processedFiles.Count);

            // verify that each file was only processed once
            Assert.True(expectedFiles.OrderBy(p => p).SequenceEqual(processedFiles.OrderBy(p => p)));
            Assert.Equal(expectedFiles.Count * 2, Directory.GetFiles(testFileDir).Length);

            // verify contents of each status file
            FileProcessor processor = listeners[0].Processor;
            foreach (string processedFile in processedFiles)
            {
                string statusFilePath = processor.GetStatusFile(Path.Combine(testFileDir, processedFile));

                string[] statusLines = File.ReadAllLines(statusFilePath);

                Assert.Equal(2, statusLines.Length);
                StatusFileEntry statusEntry = JsonConvert.DeserializeObject<StatusFileEntry>(statusLines[0]);
                Assert.Equal(ProcessingState.Processing, statusEntry.State);
                Assert.Equal(WatcherChangeTypes.Created, statusEntry.ChangeType);

                statusEntry = JsonConvert.DeserializeObject<StatusFileEntry>(statusLines[1]);
                Assert.Equal(ProcessingState.Processed, statusEntry.State);
                Assert.Equal(WatcherChangeTypes.Created, statusEntry.ChangeType);
            }

            // Now test concurrency handling for updates by updating some files
            // and verifying the updates are only processed once
            string[] filesToUpdate = processedFiles.Take(50).Select(p => Path.Combine(testFileDir, p)).ToArray();
            string item;
            while (!processedFiles.IsEmpty)
            {
                processedFiles.TryTake(out item);
            }
            await Task.Delay(1000);
            foreach (string fileToUpdate in filesToUpdate)
            {
                await Task.Delay(50);
                File.AppendAllText(fileToUpdate, "update");
            }

            // wait for all files to be processed
            await TestHelpers.Await(() =>
            {
                return processedFiles.Count >= filesToUpdate.Length;
            });
            Assert.Equal(filesToUpdate.Length, processedFiles.Count);
            Assert.Equal(expectedFiles.Count * 2, Directory.GetFiles(testFileDir).Length);

            // verify the status files are correct for each of the updated files
            foreach (string updatedFile in filesToUpdate)
            {
                string statusFilePath = processor.GetStatusFile(updatedFile);

                string[] statusLines = File.ReadAllLines(statusFilePath);

                Assert.Equal(4, statusLines.Length);
                StatusFileEntry statusEntry = JsonConvert.DeserializeObject<StatusFileEntry>(statusLines[0]);
                Assert.Equal(ProcessingState.Processing, statusEntry.State);
                Assert.Equal(WatcherChangeTypes.Created, statusEntry.ChangeType);

                statusEntry = JsonConvert.DeserializeObject<StatusFileEntry>(statusLines[1]);
                Assert.Equal(ProcessingState.Processed, statusEntry.State);
                Assert.Equal(WatcherChangeTypes.Created, statusEntry.ChangeType);

                statusEntry = JsonConvert.DeserializeObject<StatusFileEntry>(statusLines[2]);
                Assert.Equal(ProcessingState.Processing, statusEntry.State);
                Assert.Equal(WatcherChangeTypes.Changed, statusEntry.ChangeType);

                statusEntry = JsonConvert.DeserializeObject<StatusFileEntry>(statusLines[3]);
                Assert.Equal(ProcessingState.Processed, statusEntry.State);
                Assert.Equal(WatcherChangeTypes.Changed, statusEntry.ChangeType);
            }

            // Now clean up all processed files
            processor.CleanupProcessedFiles();
            Assert.Equal(0, Directory.GetFiles(testFileDir).Length);

            foreach (FileListener listener in listeners)
            {
                listener.Dispose();
            }
        }

        /// <summary>
        /// Regression test for issue #91
        /// 
        /// Previously for a *.* filter we'd process our own .status files, which would cause an infinite loop.
        /// </summary>
        [Fact]
        public async Task CatchAllFilter_AutoDelete_ProcessesAndDeletesFiles()
        {
            Mock<ITriggeredFunctionExecutor> mockExecutor = new Mock<ITriggeredFunctionExecutor>(MockBehavior.Strict);
            ConcurrentBag<string> processedFiles = new ConcurrentBag<string>();
            FunctionResult result = new FunctionResult(true);
            mockExecutor.Setup(p => p.TryExecuteAsync(It.IsAny<TriggeredFunctionData>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);

            FilesConfiguration config = new FilesConfiguration()
            {
                RootPath = rootPath
            };
            FileTriggerAttribute attribute = new FileTriggerAttribute(attributeSubPath, changeTypes: WatcherChangeTypes.Created, filter: "*.*", autoDelete: true);

            FileListener listener = new FileListener(config, attribute, mockExecutor.Object, new TestTraceWriter());
            await listener.StartAsync(CancellationToken.None);

            // create a few files with different extensions
            WriteTestFile("jpg");
            WriteTestFile("txt");
            WriteTestFile("png");

            // wait for the files to be processed fully and all files deleted (autoDelete = true)
            await TestHelpers.Await(() =>
            {
                return Directory.EnumerateFiles(testFileDir).Count() == 0;
            });

            listener.Dispose();
        }

        private void DeleteTestFiles(string path)
        {
            foreach (string file in Directory.GetFiles(path))
            {
                File.Delete(file);
            }

            TestHelpers.Await(() =>
            {
                return Directory.GetFiles(path).Length == 0;
            }).Wait();
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
}
