// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Cassandra;
using Cassandra.Mapping;
//using Microsoft.Azure.Documents;
//using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs.Extensions.Tests;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDBCassandra.Tests
{
    // The EndToEnd tests require the AzureWebJobsCosmosDBConnectionString environment variable to be set.
    [Trait("Category", "E2E")]
    public class CosmosDBEndToEndTests
    {
        private const string DatabaseName = "uprofile";
        private const string CollectionName = "user";
        private readonly TestLoggerProvider _loggerProvider = new TestLoggerProvider();

        private string user = "cassandra-3";
        private string password = "2wVREKBUPzccE4PGrFiwmIqvKj0gUI08rGRQJkS1gPnTSDJ6s0gbWNDZZ0WkSWqAT0r6eQAdkxABO2tY8Shgig==";
        private string contactpoint = "cassandra-3.cassandra.cosmos.azure.com";

        private static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                return true;
            }

            // Do not allow this client to communicate with unauthenticated servers.
            throw new InvalidOperationException($"Certificate error: {sslPolicyErrors}");

        }

        [Fact]
        public async Task CosmosDBEndToEnd()
        {
            using (var host = await StartHostAsync(typeof(EndToEndTestClass)))
            {
                //var client = await InitializeDocumentClientAsync(host.Services.GetRequiredService<IConfiguration>());

                // Call the outputs function directly, which will write out 3 documents 
                // using with the 'input' property set to the value we provide.
                var input = Guid.NewGuid().ToString();
                var parameter = new Dictionary<string, object>();
                parameter["input"] = input;

                await host.GetJobHost().CallAsync(nameof(EndToEndTestClass.Outputs), parameter);

                // Also insert a new Document so we can query on it.
                //var collectionUri = UriFactory.CreateDocumentCollectionUri(DatabaseName, CollectionName);
                //var response = await client.UpsertDocumentAsync(collectionUri, new Document());

                // Now craft a queue message to send to the Inputs, which will pull these documents.
                var queueInput = new QueueItem
                {
                    DocumentId = "1",
                    Input = input
                };

                parameter.Clear();
                parameter["item"] = JsonConvert.SerializeObject(queueInput);
                SSLOptions options = new SSLOptions(SslProtocols.Tls12, true, ValidateServerCertificate);
                options.SetHostNameResolver((ipAddress) => contactpoint);
                Cluster cluster = Cluster.Builder().WithCredentials(user, password).WithPort(10350).AddContactPoint(contactpoint).WithSSL(options).Build();
                ISession session = cluster.Connect();
                session = cluster.Connect("uprofile");
                session.Execute("CREATE TABLE IF NOT EXISTS uprofile.user (user_id int PRIMARY KEY, user_name text, user_bcity text)");
                IMapper mapper = new Mapper(session);
                mapper.Insert<User>(new User(1, "field1", "field2"));

                await host.GetJobHost().CallAsync(nameof(EndToEndTestClass.Outputs), parameter);

                await TestHelpers.Await(() =>
                {
                    return _loggerProvider.GetAllLogMessages().Count(p => p.FormattedMessage != null && p.FormattedMessage.Contains("Trigger called!")) == 4;
                });

                // Make sure the Options were logged. Just check a few values.
                string optionsMessage = _loggerProvider.GetAllLogMessages()
                    .Single(m => m.Category == "Microsoft.Azure.WebJobs.Hosting.OptionsLoggingService" && m.FormattedMessage.StartsWith("blah"))
                    .FormattedMessage;
                JObject loggedOptions = JObject.Parse(optionsMessage.Substring(optionsMessage.IndexOf(Environment.NewLine)));
                Assert.Null(loggedOptions["ConnectionMode"].Value<string>());
                Assert.False(loggedOptions["LeaseOptions"]["CheckpointFrequency"]["ExplicitCheckpoint"].Value<bool>());
                Assert.Equal(TimeSpan.FromSeconds(5).ToString(), loggedOptions["LeaseOptions"]["FeedPollDelay"].Value<string>());
            }
        }

        //private async Task<DocumentClient> InitializeDocumentClientAsync(IConfiguration configuration)
        //{
        //    var builder = new DbConnectionStringBuilder
        //    {
        //        ConnectionString = configuration.GetConnectionString(Constants.DefaultConnectionStringName)
        //    };

        //    var serviceUri = new Uri(builder["AccountEndpoint"].ToString());
        //    var client = new DocumentClient(serviceUri, builder["AccountKey"].ToString());

        //    var database = new Database() { Id = DatabaseName };
        //    await client.CreateDatabaseIfNotExistsAsync(database);

        //    var collection = new DocumentCollection() { Id = CollectionName };
        //    await client.CreateDocumentCollectionIfNotExistsAsync(UriFactory.CreateDatabaseUri(DatabaseName), collection);

        //    return client;
        //}

        private async Task<IHost> StartHostAsync(Type testType)
        {
            ExplicitTypeLocator locator = new ExplicitTypeLocator(testType);

            IHost host = new HostBuilder()
                .ConfigureWebJobs(builder =>
                {
                    builder
                    .AddAzureStorage()
                    .AddCosmosDBCassandra();
                })
                .ConfigureAppConfiguration(c =>
                {
                    c.AddTestSettings();
                })
                .ConfigureServices(services =>
                {
                    services.AddSingleton<ITypeLocator>(locator);
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddProvider(_loggerProvider);
                })
                .Build();

            await host.StartAsync();
            return host;
        }

        public class QueueItem
        {
            public string DocumentId { get; set; }

            public string Input { get; set; }
        }

        private class User
        {
            public int user_id { get; set; }

            public String user_name { get; set; }

            public String user_bcity { get; set; }

            public User(int user_id, String user_name, String user_bcity)
            {
                this.user_id = user_id;
                this.user_name = user_name;
                this.user_bcity = user_bcity;
            }

            public override String ToString()
            {
                return String.Format(" {0} | {1} | {2} ", user_id, user_name, user_bcity);
            }
        }



        private static class EndToEndTestClass
        {
            [NoAutomaticTrigger]
            public static async Task Outputs(
                string input,
                [CosmosDBCassandraTrigger(DatabaseName, CollectionName)] IAsyncCollector<object> collector,
                ILogger log)
            {
                for (int i = 0; i < 3; i++)
                {
                    await collector.AddAsync(new { input });
                }
            }

            [NoAutomaticTrigger]
            public static void Inputs(
                [QueueTrigger("NotUsed")] QueueItem item,
                [CosmosDBCassandraTrigger(DatabaseName, CollectionName)] JObject document,
                [CosmosDBCassandraTrigger(DatabaseName, CollectionName)] IReadOnlyList<JArray> documents,
                ILogger log)
            {
                Assert.NotNull(document);
                Assert.Equal(3, documents.Count());
            }

            public static void Trigger(
                [CosmosDBCassandraTrigger(DatabaseName, CollectionName)]IReadOnlyList<JArray> documents,
                ILogger log)
            {
                foreach (var document in documents)
                {
                    System.Diagnostics.Debug.WriteLine("Cassandra row: " + document.ToString());
                    log.LogInformation("Trigger called!");
                }
            }
        }
    }
}
