// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.ApiHub;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.ApiHub
{
    public class ApiHubTestFixture : IDisposable
    {
        public const string HostContainerName = "azure-webjobs-hosts";
        public const string PoisonQueueName = "webjobs-apihubtrigger-poison";
        public const string ApiHubBlobDirectoryPathTemplate = "apihubs/{0}";

        public const string ImportTestPath = @"import";
        public const string OutputTestPath = @"output";
        public const string ExceptionPath = @"exceptionPath";
        public const string PathsTestPath = @"paths";

        public ApiHubTestFixture()
        {
            // The default ConnectionLimit which is 2 needs to be increased for ApiHub file tests as there are many triggers calling to the same end point.
            ServicePointManager.DefaultConnectionLimit = 30;

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AzureWebJobsDropBox")))
            {
                ApiHubConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsDropBox");
            }
            else
            {
                ApiHubConnectionString = "UseLocalFileSystem=true;Path=" + Path.GetTempPath() + "ApiHubDropBox";
            }

            ExplicitTypeLocator locator = new ExplicitTypeLocator(typeof(ApiHubFileTestJobs));

            // Use MachineName as the host Id
            var machineName = Environment.MachineName.ToLower(CultureInfo.InvariantCulture);
            if (machineName.Length > 30)
            {
                machineName = machineName.Substring(0, 30);
            }

            Config = new JobHostConfiguration
            {
                TypeLocator = locator,
                HostId = machineName,
            };

            RootFolder = ItemFactory.Parse(ApiHubConnectionString);

            CloudStorageAccount account = CloudStorageAccount.Parse(Config.StorageConnectionString);
            CloudBlobClient blobClient = account.CreateCloudBlobClient();

            string apiHubBlobDirectoryPath = string.Format(ApiHubBlobDirectoryPathTemplate, Config.HostId);
            ApiHubBlobDirectory = blobClient.GetContainerReference(HostContainerName).GetDirectoryReference(apiHubBlobDirectoryPath);
            CloudQueueClient queueClient = CloudStorageAccount.Parse(this.Config.StorageConnectionString).CreateCloudQueueClient();
            this.PoisonQueue = queueClient.GetQueueReference(PoisonQueueName);
            this.PoisonQueue.CreateIfNotExists();

            CreateFolder(ImportTestPath).Wait();
            CreateFolder(ExceptionPath).Wait();
            CreateFolder(PathsTestPath).Wait();

            DeleteExistingArtifcats();

            this.Serializer = JsonSerializer.Create();

             this.TraceWriter = new TestTraceWriter(System.Diagnostics.TraceLevel.Verbose);
             Config.Tracing.Tracers.Add(this.TraceWriter);
        }

        public IFolderItem RootFolder { get; private set; }
        public string ApiHubConnectionString { get; private set; }
        public CloudBlobDirectory ApiHubBlobDirectory { get; private set; }
        public CloudQueue PoisonQueue { get; private set; }
        public JobHostConfiguration Config { get; private set; }
        public JsonSerializer Serializer { get; private set; }
        public TestTraceWriter TraceWriter { get; private set; }

        public async Task<IFileItem> WriteTestFile(string extension = "txt", string path = null)
        {
            string filePath;
            if (path != null)
            {
                filePath = path;
            }
            else
            {
                filePath = ImportTestPath;
            }
            string testFileName = string.Format("{0}.{1}", Guid.NewGuid(), extension);
            string testFilePath = filePath + "/" + testFileName;

            var file = RootFolder.GetFileReference(testFilePath, true);
            await file.WriteAsync(new byte[] { 0, 1, 2, 3 });

            return file;
        }

        private void DeleteApiHubBlobs()
        {
            foreach (var blob in this.ApiHubBlobDirectory.ListBlobs())
            {
                var blockBlob = blob as CloudBlockBlob;

                if (blockBlob != null)
                {
                    blockBlob.DeleteIfExists();
                }
            }
        }

        private async Task CreateFolder(string folderName)
        {
            var folder = RootFolder.GetFolderReference(folderName);

            if (!await folder.FolderExistsAsync(folderName))
            {
                // write a test file to create the folder if it doesn't exist.
                await WriteTestFile(path: folderName);
            }
        }

        private async Task EmptyFolder(string folderName)
        {
            var folder = RootFolder.GetFolderReference(folderName);

            foreach (var item in await folder.ListAsync(true))
            {
                var i = item as IFileItem;
                if (i != null)
                {
                    await i.DeleteAsync();
                }
            }
        }

        private void DeleteExistingArtifcats()
        {
            EmptyFolder(ImportTestPath).Wait();
            EmptyFolder(OutputTestPath).Wait();
            EmptyFolder(ExceptionPath).Wait();
            EmptyFolder(PathsTestPath).Wait();

            DeleteApiHubBlobs();

            if (this.PoisonQueue.Exists())
            {
                this.PoisonQueue.Clear();
            }
        }
        public void Dispose()
        {
            DeleteExistingArtifcats();
        }
    }
}
