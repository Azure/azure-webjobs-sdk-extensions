// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.ApiHub;
using Microsoft.Azure.WebJobs.Extensions.ApiHub;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.ApiHub
{
    public class ApiHubBindingEndToEndTests
    {
        private const string HostContainerName = "azure-webjobs-hosts";
        private const string ApiHubBlobDirectoryPathTemplate = "apihubs/{0}";

        private const string ImportTestPath = @"import";
        private const string OutputTestPath = @"output";

        private string _apiHubConnectionString;
        private IFolderItem _rootFolder;
        private CloudBlobDirectory _apiHubBlobDirectory;

        private JobHostConfiguration _config;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1308:NormalizeStringsToUppercase")]
        public ApiHubBindingEndToEndTests()
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AzureWebJobsDropBox")))
            {
                _apiHubConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsDropBox");
            }
            else
            {
                _apiHubConnectionString = "UseLocalFileSystem=true;Path=" + Path.GetTempPath() + "ApiHubDropBox";
            }

            ExplicitTypeLocator locator = new ExplicitTypeLocator(typeof(ApiHubTestJobs));

            // Use MachineName as the host Id
            var machineName = Environment.MachineName.ToLower(CultureInfo.InvariantCulture);
            if (machineName.Length > 30)
            {
                machineName = machineName.Substring(0, 30);
            }

            _config = new JobHostConfiguration
            {
                TypeLocator = locator,
                HostId = machineName,
            };

            _rootFolder = ItemFactory.Parse(_apiHubConnectionString);

            var folder = _rootFolder.GetFolderReferenceAsync(ImportTestPath).GetAwaiter().GetResult();

            if (!folder.FolderExists(ImportTestPath))
            {
                // write a test file to create the folder if it doesn't exist.
                WriteTestFile().GetAwaiter().GetResult();
            }

            foreach (var item in folder.ListAsync(true).GetAwaiter().GetResult())
            {
                var i = item as IFileItem;
                if (i != null)
                {
                    i.DeleteAsync();
                }
            }

            folder = _rootFolder.GetFolderReferenceAsync(OutputTestPath).GetAwaiter().GetResult();
            foreach (var item in folder.ListAsync(true).GetAwaiter().GetResult())
            {
                var i = item as IFileItem;
                if (i != null)
                {
                    i.DeleteAsync();
                }
            }

            ApiHubTestJobs.Processed.Clear();

            CloudStorageAccount account = CloudStorageAccount.Parse(_config.StorageConnectionString);
            CloudBlobClient blobClient = account.CreateCloudBlobClient();

            string apiHubBlobDirectoryPath = string.Format(ApiHubBlobDirectoryPathTemplate, _config.HostId);
            _apiHubBlobDirectory = blobClient.GetContainerReference(HostContainerName).GetDirectoryReference(apiHubBlobDirectoryPath);

            DeleteApiHubBlobs();
        }

        [Fact]
        public async void JobIsTriggeredForNewFiles()
        {
            JobHost host = CreateTestJobHost();

            host.Start();

            Assert.Equal(0, ApiHubTestJobs.Processed.Count);

            // now write a file to trigger the job
            var fileItem = await WriteTestFile();

            await TestHelpers.Await(() =>
            {
                return _rootFolder.FileExists(fileItem.Path);
            });

            await TestHelpers.Await(() =>
            {
                return ApiHubTestJobs.Processed.Count != 0;
            });

            Assert.Equal(1, ApiHubTestJobs.Processed.Count);
            Assert.Equal(Path.GetFileName(fileItem.Path), ApiHubTestJobs.Processed.Single());
            host.Stop();

            await fileItem.DeleteAsync();
        }

        [Fact]
        public async void ChecksRelatedBlobsGettingUpdated()
        {
            JobHost host = CreateTestJobHost();

            host.Start();

            // now write a file to trigger the job
            var fileItem = await WriteTestFile();

            await TestHelpers.Await(() =>
            {
                return _rootFolder.FileExists(fileItem.Path);
            });

            await TestHelpers.Await(() =>
            {
                return ApiHubTestJobs.Processed.Count != 0;
            });

            var apiHubBlob = GetBlobReference("ImportTestJob");

            Assert.True(await apiHubBlob.ExistsAsync());
            var content = await apiHubBlob.DownloadTextAsync();

            Assert.True(!string.IsNullOrEmpty(content));

            // waiting for 1 sec to make sure we get an updated DateTime for the blob entry which is in the HH:mm:ss format for local files. 
            await Task.Delay(1000);
            ApiHubTestJobs.Processed.Clear();

            // now write a 2nd file to trigger the job and making sure the blob is updated
            var fileItem2 = await WriteTestFile();

            await TestHelpers.Await(() =>
            {
                return _rootFolder.FileExists(fileItem2.Path);
            });

            await TestHelpers.Await(() =>
            {
                return ApiHubTestJobs.Processed.Count != 0;
            });

            var content2 = await apiHubBlob.DownloadTextAsync();

            Assert.False(content2.Equals(content, StringComparison.OrdinalIgnoreCase));

            host.Stop();

            await fileItem.DeleteAsync();
            await fileItem2.DeleteAsync();
        }

        [Fact]
        public async Task ApiHubAttribute_SupportsExpectedOutputBindings()
        {
            JobHost host = CreateTestJobHost();
            host.Start();

            await VerifyOutputBinding(typeof(ApiHubTestJobs).GetMethod("BindToStringOutput"));
            await VerifyOutputBinding(typeof(ApiHubTestJobs).GetMethod("BindToByteArrayOutput"));
            await VerifyOutputBinding(typeof(ApiHubTestJobs).GetMethod("BindToStreamOutput"));
            await VerifyOutputBinding(typeof(ApiHubTestJobs).GetMethod("BindToStreamWriterOutput"));
            await VerifyOutputBinding(typeof(ApiHubTestJobs).GetMethod("BindToTextWriterOutput"));

            host.Stop();
        }

        [Fact]
        public async Task ApiHubAttribute_SupportsExpectedInputBindings()
        {
            JobHost host = CreateTestJobHost();
            host.Start();

            await VerifyInputBinding(host, typeof(ApiHubTestJobs).GetMethod("BindToStringInput"));
            await VerifyInputBinding(host, typeof(ApiHubTestJobs).GetMethod("BindToByteArrayInput"));
            await VerifyInputBinding(host, typeof(ApiHubTestJobs).GetMethod("BindToStreamInput"));
            await VerifyInputBinding(host, typeof(ApiHubTestJobs).GetMethod("BindToStreamReaderInput"));
            await VerifyInputBinding(host, typeof(ApiHubTestJobs).GetMethod("BindToTextReaderInput"));

            host.Stop();
        }

        [Fact]
        public async Task ManualBindToString()
        {
            JobHost host = CreateTestJobHost();

            var method = typeof(ApiHubTestJobs).GetMethod("BindToStringOutput");

            string data = Guid.NewGuid().ToString();
            string inputFileName = ImportTestPath + "/BindToString.txt";

            var inputFile = await _rootFolder.GetFileReferenceAsync(inputFileName, true);
            await inputFile.WriteAsync(Encoding.UTF8.GetBytes(data));

            host.Call(method, new { input = inputFileName });

            string outputFileName = OutputTestPath + "/BindToString.txt";
            var outputFile = await _rootFolder.GetFileReferenceAsync(outputFileName, true);

            await TestHelpers.Await(() =>
            {
                return _rootFolder.FileExists(outputFileName);
            });

            var result = string.Empty;

            await TestHelpers.Await(() =>
            {
                // sometime there is a delay between a file being created in a SAAS provider and its content being non-empty. hence adding this logic.
                result = Encoding.UTF8.GetString(outputFile.ReadAsync().GetAwaiter().GetResult());
                return !string.IsNullOrEmpty(result);
            });

            Assert.Equal(data, result);
        }

        private async Task VerifyInputBinding(JobHost host, MethodInfo method)
        {
            string data = Guid.NewGuid().ToString();
            string inputFileName = ImportTestPath + "/" + string.Format("{0}.txt", method.Name);

            var inputFile = await _rootFolder.GetFileReferenceAsync(inputFileName, true);
            await inputFile.WriteAsync(Encoding.UTF8.GetBytes(data));

            host.Call(method);

            string outputFileName = OutputTestPath + "/" + string.Format("{0}.txt", method.Name);
            var outputFile = await _rootFolder.GetFileReferenceAsync(outputFileName, true);

            await TestHelpers.Await(() =>
            {
                return _rootFolder.FileExists(outputFileName);
            });

            var result = string.Empty;

            await TestHelpers.Await(() =>
            {
                // sometime there is a delay between a file being created in a SAAS provider and its content being non-empty. hence adding this logic.
                result = Encoding.UTF8.GetString(outputFile.ReadAsync().GetAwaiter().GetResult());
                return !string.IsNullOrEmpty(result);
            });

            Assert.Equal(data, result);
        }

        private async Task VerifyOutputBinding(MethodInfo method)
        {
            string data = Guid.NewGuid().ToString();
            string inputFileName = ImportTestPath + "/" + string.Format("{0}.txt", method.Name);

            var inputFile = await _rootFolder.GetFileReferenceAsync(inputFileName, true);
            await inputFile.WriteAsync(Encoding.UTF8.GetBytes(data));

            string outputFileName = OutputTestPath + "/" + string.Format("{0}.txt", method.Name);
            var outputFile = await _rootFolder.GetFileReferenceAsync(outputFileName, true);

            await TestHelpers.Await(() =>
            {
                return _rootFolder.FileExists(outputFileName);
            });

            var result = string.Empty;

            await TestHelpers.Await(() =>
            {
                // sometime there is a delay between a file being created in a SAAS provider and its content being non-empty. hence adding this logic.
                result = Encoding.UTF8.GetString(outputFile.ReadAsync().GetAwaiter().GetResult());
                return !string.IsNullOrEmpty(result);
            });

            Assert.Equal(data, result);
        }

        private JobHost CreateTestJobHost()
        {
            var apiHubConfig = new ApiHubConfiguration();

            apiHubConfig.AddConnection("dropbox", _apiHubConnectionString);
            _config.UseApiHub(apiHubConfig);

            return new JobHost(_config);
        }

        private void DeleteTestFiles(string path)
        {
            foreach (string file in Directory.GetFiles(path))
            {
                File.Delete(file);
            }
        }

        private void DeleteApiHubBlobs()
        {
            foreach (var blob in this._apiHubBlobDirectory.ListBlobs())
            {
                var blockBlob = blob as CloudBlockBlob;

                if (blockBlob != null)
                {
                    blockBlob.DeleteIfExists();
                }
            }
        }

        private async Task<IFileItem> WriteTestFile(string extension = "txt")
        {
            string testFileName = string.Format("{0}.{1}", Guid.NewGuid(), extension);
            string testFilePath = ImportTestPath + "/" + testFileName;

            var file = await _rootFolder.GetFileReferenceAsync(testFilePath, true);
            await file.WriteAsync(new byte[] { 0, 1, 2, 3 });

            Assert.True(_rootFolder.FileExists(file.Path));
            return file;
        }

        private CloudBlockBlob GetBlobReference(string functionName)
        {
            // Path to apiHub blob is:
            // apihubs/{siteName}/{functionName}/status
            string blobName = string.Format("{0}/status", functionName);
            return this._apiHubBlobDirectory.GetBlockBlobReference(blobName);
        }

        public static class ApiHubTestJobs
        {
            static ApiHubTestJobs()
            {
                Processed = new List<string>();
            }

            public static List<string> Processed { get; private set; }

            public static void ImportTestJob(
                [ApiHubFileTrigger("dropbox", "import/{name}", PollIntervalInSeconds = 1)] Stream sr,
                string name)
            {
                Processed.Add(name);
                sr.Close();
            }

            public static void BindToStringOutput(
                [ApiHubFileTrigger("dropbox", ImportTestPath + @"/{name}", PollIntervalInSeconds = 1)] string input,
                [ApiHubFile("dropbox", OutputTestPath + @"/{name}", FileAccess.Write)] out string output)
            {
                output = input;
            }

            public static void BindToByteArrayOutput(
                [ApiHubFileTrigger("dropbox", ImportTestPath + @"/{name}", PollIntervalInSeconds = 1)] string input,
                [ApiHubFile("dropbox", OutputTestPath + @"/{name}", FileAccess.Write)] out byte[] output)
            {
                output = Encoding.UTF8.GetBytes(input);
            }

            public static void BindToStreamOutput(
                [ApiHubFileTrigger("dropbox", ImportTestPath + @"/{name}", PollIntervalInSeconds = 1)] string input,
                [ApiHubFile("dropbox", OutputTestPath + @"/{name}", FileAccess.Write)] Stream output)
            {
                StreamWriter sw = new StreamWriter(output);
                sw.Write(input);

                // TODO: this test will fail if we call Close() here. needs more investigation.
                //sw.Close();
            }

            public static void BindToStreamWriterOutput(
                [ApiHubFileTrigger("dropbox", ImportTestPath + @"/{name}", PollIntervalInSeconds = 1)] Stream input,
                [ApiHubFile("dropbox", OutputTestPath + @"/{name}", FileAccess.Write)] StreamWriter output)
            {
                using (StreamReader reader = new StreamReader(input))
                {
                    string text = reader.ReadToEnd();
                    output.Write(text);
                }
            }

            public static void BindToTextWriterOutput(
                [ApiHubFileTrigger("dropbox", ImportTestPath + @"/{name}", PollIntervalInSeconds = 1)] StreamReader input,
                [ApiHubFile("dropbox", OutputTestPath + @"/{name}", FileAccess.Write)] TextWriter output)
            {
                output.Write(input.ReadToEnd());
            }

            public static void BindToStreamInput(
                [ApiHubFile("dropbox", ImportTestPath + @"/BindToStreamInput.txt")] Stream input,
                [ApiHubFile("dropbox", OutputTestPath + @"/BindToStreamInput.txt", FileAccess.Write)] Stream output)
            {
                input.CopyTo(output);
            }

            public static void BindToStreamReaderInput(
                [ApiHubFile("dropbox", ImportTestPath + @"/BindToStreamReaderInput.txt")] StreamReader input,
                [ApiHubFile("dropbox", OutputTestPath + @"/BindToStreamReaderInput.txt", FileAccess.Write)] out string output)
            {
                output = input.ReadToEnd();
            }

            public static void BindToTextReaderInput(
                [ApiHubFile("dropbox", ImportTestPath + @"/BindToTextReaderInput.txt")] TextReader input,
                [ApiHubFile("dropbox", OutputTestPath + @"/BindToTextReaderInput.txt", FileAccess.Write)] out string output)
            {
                output = input.ReadToEnd();
            }

            public static void BindToStringInput(
                [ApiHubFile("dropbox", ImportTestPath + "/BindToStringInput.txt")] string input,
                [ApiHubFile("dropbox", OutputTestPath + "/BindToStringInput.txt", FileAccess.Write)] out string output)
            {
                output = input;
            }

            public static void BindToByteArrayInput(
                [ApiHubFile("dropbox", ImportTestPath + @"/BindToByteArrayInput.txt")] byte[] input,
                [ApiHubFile("dropbox", OutputTestPath + @"/BindToByteArrayInput.txt", FileAccess.Write)] out string output)
            {
                output = Encoding.UTF8.GetString(input);
            }
        }
    }
}
