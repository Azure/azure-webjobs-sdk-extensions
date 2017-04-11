// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.ApiHub;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.WindowsAzure.Storage.Blob;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.ApiHub
{
    [Trait("Category", "E2E")]
    public class ApiHubBindingEndToEndTests : IClassFixture<ApiHubTestFixture>, IDisposable
    {
        private ApiHubTestFixture _fixture;

        public ApiHubBindingEndToEndTests(ApiHubTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async void JobIsTriggeredForNewFiles()
        {
            JobHost host = CreateTestJobHost();
            await host.StartAsync();

            int count = ApiHubFileTestJobs.Processed.Count;

            // now write a file to trigger the job
            var fileItem = await _fixture.WriteTestFile();

            await TestHelpers.Await(() =>
            {
                return _fixture.RootFolder.FileExistsAsync(fileItem.Path).GetAwaiter().GetResult();
            });

            await TestHelpers.Await(() =>
            {
                return ApiHubFileTestJobs.Processed.Count > count;
            });

            Assert.True(ApiHubFileTestJobs.Processed.Contains(Path.GetFileName(fileItem.Path)));

            await host.StopAsync();

            await fileItem.DeleteAsync();
        }

        [Fact]
        public async void PathsTemplateCheck()
        {
            var fileName = "pathstemplatestest.txt";

            JobHost host = CreateTestJobHost();
            await host.StartAsync();

            // now write a file to trigger the job
            string data = Guid.NewGuid().ToString();
            string inputFileName = ApiHubTestFixture.PathsTestPath + "/" + fileName;

            var inputFile = _fixture.RootFolder.GetFileReference(inputFileName, true);
            await inputFile.WriteAsync(Encoding.UTF8.GetBytes(data));

            // verifying both PathsTestJob1 and PathsTestJob2 get called. 
            await VerifyOutputBinding(data, string.Format("{0}.path1", fileName));
            await VerifyOutputBinding(data, string.Format("{0}.path2", fileName));

            await host.StopAsync();
        }

        [Fact]
        public async void ChecksRelatedBlobsGettingUpdated()
        {
            JobHost host = CreateTestJobHost();
            await host.StartAsync();

            // now write a file to trigger the job
            var fileItem = await _fixture.WriteTestFile();

            await TestHelpers.Await(() =>
            {
                return _fixture.RootFolder.FileExistsAsync(fileItem.Path).GetAwaiter().GetResult();
            });

            await TestHelpers.Await(() =>
            {
                return ApiHubFileTestJobs.Processed.Count != 0;
            });

            var apiHubBlob = GetBlobReference("ImportTestJob");

            Assert.True(await apiHubBlob.ExistsAsync());
            var content = await apiHubBlob.DownloadTextAsync();

            Assert.True(!string.IsNullOrEmpty(content));

            // waiting for 1 sec to make sure we get an updated DateTime for the blob entry which is in the HH:mm:ss format for local files. 
            await Task.Delay(1000);
            ApiHubFileTestJobs.Processed.Clear();

            // now write a 2nd file to trigger the job and making sure the blob is updated
            var fileItem2 = await _fixture.WriteTestFile();

            await TestHelpers.Await(() =>
            {
                return _fixture.RootFolder.FileExistsAsync(fileItem2.Path).GetAwaiter().GetResult();
            });

            await TestHelpers.Await(() =>
            {
                return ApiHubFileTestJobs.Processed.Count != 0;
            });

            var content2 = await apiHubBlob.DownloadTextAsync();

            Assert.False(content2.Equals(content, StringComparison.OrdinalIgnoreCase));

            await host.StopAsync();

            await fileItem.DeleteAsync();
            await fileItem2.DeleteAsync();
        }

        [Fact]
        public async void ChecksPoisonQueueGettingPopulated()
        {
            JobHost host = CreateTestJobHost();
            await host.StartAsync();

            // now write a file to trigger the job
            var fileItem = await _fixture.WriteTestFile(path: ApiHubTestFixture.ExceptionPath);

            await TestHelpers.Await(() =>
            {
                return _fixture.RootFolder.FileExistsAsync(fileItem.Path).GetAwaiter().GetResult();
            });

            await TestHelpers.Await(() =>
            {
                return _fixture.PoisonQueue.Exists();
            });

            await TestHelpers.Await(() =>
            {
                return _fixture.PoisonQueue.PeekMessage() != null;
            });

            var message = await _fixture.PoisonQueue.GetMessageAsync();

            Assert.True(!string.IsNullOrEmpty(message.AsString));

            ApiHubFileInfo status;
            using (StringReader stringReader = new StringReader(message.AsString))
            {
                status = (ApiHubFileInfo)_fixture.Serializer.Deserialize(stringReader, typeof(ApiHubFileInfo));
            }

            Assert.Equal("ThrowException", status.FunctionName);
            Assert.Equal(Path.GetFileName(fileItem.Path), Path.GetFileName(status.FilePath));
            Assert.Equal("dropbox", status.Connection);

            await host.StopAsync();

            await fileItem.DeleteAsync();
        }

        [Fact]
        public async Task ApiHubAttribute_SupportsExpectedOutputBindings()
        {
            var fileName = "bindtooutputtypes.txt";
            JobHost host = CreateTestJobHost();
            await host.StartAsync();

            string data = Guid.NewGuid().ToString();
            string inputFileName = ApiHubTestFixture.ImportTestPath + "/" + fileName;

            var inputFile = _fixture.RootFolder.GetFileReference(inputFileName, true);
            await inputFile.WriteAsync(Encoding.UTF8.GetBytes(data));

            await VerifyOutputBinding(data, string.Format("{0}.string", fileName));
            await VerifyOutputBinding(data, string.Format("{0}.byte", fileName));
            await VerifyOutputBinding(data, string.Format("{0}.stream", fileName));
            await VerifyOutputBinding(data, string.Format("{0}.streamWriter", fileName));
            await VerifyOutputBinding(data, string.Format("{0}.textWriter", fileName));

            await host.StopAsync();
        }

        [Fact]
        public async Task ApiHubAttribute_SupportsExpectedInputBindings()
        {
            JobHost host = CreateTestJobHost();
            await host.StartAsync();

            await VerifyInputBinding(host, typeof(ApiHubFileTestJobs).GetMethod("BindToStringInput"));
            await VerifyInputBinding(host, typeof(ApiHubFileTestJobs).GetMethod("BindToByteArrayInput"));
            await VerifyInputBinding(host, typeof(ApiHubFileTestJobs).GetMethod("BindToStreamInput"));
            await VerifyInputBinding(host, typeof(ApiHubFileTestJobs).GetMethod("BindToStreamReaderInput"));
            await VerifyInputBinding(host, typeof(ApiHubFileTestJobs).GetMethod("BindToTextReaderInput"));

            await host.StopAsync();
        }

        [Fact]
        public async Task ManualBindToString()
        {
            JobHost host = CreateTestJobHost();

            var method = typeof(ApiHubFileTestJobs).GetMethod("BindToOutputTypes");

            string data = Guid.NewGuid().ToString();
            string inputFileName = ApiHubTestFixture.ImportTestPath + "/ManualBindToString.txt";

            var inputFile = _fixture.RootFolder.GetFileReference(inputFileName, true);
            await inputFile.WriteAsync(Encoding.UTF8.GetBytes(data));

            await host.CallAsync(method, new { input = inputFileName });

            string outputFileName = ApiHubTestFixture.OutputTestPath + "/ManualBindToString.txt.string";
            var outputFile = _fixture.RootFolder.GetFileReference(outputFileName, true);

            await TestHelpers.Await(() =>
            {
                return _fixture.RootFolder.FileExistsAsync(outputFileName).GetAwaiter().GetResult();
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

        [Fact]
        public async Task ApiHubImperativeBinding()
        {
            Environment.SetEnvironmentVariable("dropbox2", _fixture.ApiHubConnectionString);

            JobHost host = CreateTestJobHost();

            var method = typeof(ApiHubFileTestJobs).GetMethod("ImperativeBind");

            await host.CallAsync(method);

            var content = "Generated by Azure Functions";

            string outputFileName = ApiHubTestFixture.OutputTestPath + "/ImperativeBind.txt";
            var outputFile = _fixture.RootFolder.GetFileReference(outputFileName, true);

            await TestHelpers.Await(() =>
            {
                return _fixture.RootFolder.FileExistsAsync(outputFileName).GetAwaiter().GetResult();
            });

            var result = string.Empty;

            await TestHelpers.Await(() =>
            {
                // sometime there is a delay between a file being created in a SAAS provider and its content being non-empty. hence adding this logic.
                result = Encoding.UTF8.GetString(outputFile.ReadAsync().GetAwaiter().GetResult());
                return !string.IsNullOrEmpty(result);
            });

            Assert.Equal(content, result);
        }

        private async Task VerifyInputBinding(JobHost host, MethodInfo method)
        {
            string data = Guid.NewGuid().ToString();
            string inputFileName = ApiHubTestFixture.ImportTestPath + "/" + string.Format("{0}.txt", method.Name);

            var inputFile = _fixture.RootFolder.GetFileReference(inputFileName, true);
            await inputFile.WriteAsync(Encoding.UTF8.GetBytes(data));

            await host.CallAsync(method);

            string outputFileName = ApiHubTestFixture.OutputTestPath + "/" + string.Format("{0}.txt", method.Name);
            var outputFile = _fixture.RootFolder.GetFileReference(outputFileName, true);

            await TestHelpers.Await(() =>
            {
                return _fixture.RootFolder.FileExistsAsync(outputFileName).GetAwaiter().GetResult();
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

        private async Task VerifyOutputBinding(string data, string fileName)
        {
            string outputFileName = ApiHubTestFixture.OutputTestPath + "/" + fileName;
            var outputFile = _fixture.RootFolder.GetFileReference(outputFileName, true);

            await TestHelpers.Await(() =>
            {
                return _fixture.RootFolder.FileExistsAsync(outputFileName).GetAwaiter().GetResult();
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
            apiHubConfig.Logger = _fixture.TraceWriter;
            apiHubConfig.Logger.Level = System.Diagnostics.TraceLevel.Verbose;
            apiHubConfig.AddConnection("dropbox", _fixture.ApiHubConnectionString);
            apiHubConfig.MaxFunctionExecutionRetryCount = 2;

            _fixture.Config.UseApiHub(apiHubConfig);

            return new JobHost(_fixture.Config);
        }

        private CloudBlockBlob GetBlobReference(string functionName)
        {
            // Path to apiHub blob is:
            // apihubs/{siteName}/{functionName}/status
            string blobName = string.Format("{0}/status", functionName);
            return _fixture.ApiHubBlobDirectory.GetBlockBlobReference(blobName);
        }

        public void Dispose()
        {
        }

        private class ApiHubFileInfo
        {
            public string FunctionName { get; set; }

            public string FilePath { get; set; }

            public string Connection { get; set; }
        }
    }
}
