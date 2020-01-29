// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Cassandra;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDBCassandra
{
    internal class CosmosDBTriggerListener : IListener
    {
        private readonly ITriggeredFunctionExecutor _executor;
        private readonly ILogger _logger;
        private readonly string _hostName;
        private readonly ICosmosDBCassandraService _cosmosDBCassandraService;
        private readonly string _functionId;
        private readonly string _keyspace;
        private readonly string _table;
        private readonly TimeSpan _defaultTimeSpan;
        private readonly bool _startFromBeginning;
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private static ISession session;
        

        public CosmosDBTriggerListener(ITriggeredFunctionExecutor executor,
            string functionId,
            string keyspace,
            string table,
            bool startFromBeginning,
            int feedpolldelay,
            ICosmosDBCassandraService cosmosDBCassandraService,
            ILogger logger)
        {
            this._logger = logger;
            this._executor = executor;
            this._functionId = functionId;
            this._keyspace = keyspace;
            this._table = table;
            this._startFromBeginning = startFromBeginning;
            this._defaultTimeSpan = feedpolldelay > 0 ? TimeSpan.FromMilliseconds(feedpolldelay) : TimeSpan.FromMilliseconds(5000);
            this._cosmosDBCassandraService = cosmosDBCassandraService;
            this._hostName = Guid.NewGuid().ToString();
        }

        public void Cancel()
        {
            this.StopAsync(CancellationToken.None).Wait();
        }

        public void Dispose()
        {
            session.Dispose();
        }


        public async Task StartAsync(CancellationToken cancellationToken) {

            Cluster cluster = _cosmosDBCassandraService.GetCluster();

            session = cluster.Connect(_keyspace);
            //set initial start time for pulling the change feed
            
            DateTime timeBegin = this._startFromBeginning ? DateTime.MinValue.ToUniversalTime() : DateTime.UtcNow;

            //initialise variable to store the continuation token
            byte[] pageState = null;
            _logger.LogInformation(string.Format($"Reading from Cassandra API change feed..."));

            while (!cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    IStatement changeFeedQueryStatement = new SimpleStatement(
                    $"SELECT * FROM "+_keyspace+"."+_table+$" where COSMOS_CHANGEFEED_START_TIME() = '{timeBegin.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)}'");
                    if (pageState != null)
                    {
                        changeFeedQueryStatement = changeFeedQueryStatement.SetPagingState(pageState);
                    }                   
                    RowSet rowSet = session.Execute(changeFeedQueryStatement);
                    pageState = rowSet.PagingState;

                    TimeSpan wait = _defaultTimeSpan;
                    if (rowSet.IsFullyFetched) {
                        IReadOnlyList<Row> rowList = rowSet.ToList();
                        CqlColumn[] columns = rowSet.Columns;
                        if (rowList.Count != 0)
                        {
                            await _executor.TryExecuteAsync(new TriggeredFunctionData() { TriggerValue = rowList }, cancellationToken);
                            wait = TimeSpan.Zero; // If there were changes, we want to capture the next batch right away with no delay
                        }
                    }
                    
                    await Task.Delay(wait, cancellationTokenSource.Token);
                }
                catch (TaskCanceledException e) 
                {
                    _logger.LogWarning(e, "Task cancelled");
                } 
                catch (Exception e)
                {
                    _logger.LogError(e, "Error on change feed cycle");
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            cancellationTokenSource.Cancel();
            return Task.CompletedTask;
        }
        
    }
}
