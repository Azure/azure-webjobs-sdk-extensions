// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.NotificationHubs;
using Microsoft.Azure.WebJobs.Host;

namespace Microsoft.Azure.WebJobs.Extensions.NotificationHubs
{
    internal class NotificationHubAsyncCollector : IAsyncCollector<Notification>
    {
        private INotificationHubClientService _notificationHubclientService;
        private string _tagExpression;
        private bool _enableTestSend;
        private TraceWriter _traceWriter;

        public NotificationHubAsyncCollector(INotificationHubClientService clientService, string tagExpression, bool enableTestSend, TraceWriter traceWriter)
        {
            _notificationHubclientService = clientService;
            _tagExpression = tagExpression;
            _enableTestSend = enableTestSend;
            _traceWriter = traceWriter;
        }

        public async Task AddAsync(Notification item, CancellationToken cancellationToken = default(CancellationToken))
        {
            NotificationOutcome notificationOutcome = await _notificationHubclientService.SendNotificationAsync(item, _tagExpression);
            if (_enableTestSend)
            {
                string debugLog = $"NotificationHubs Test Send\r\n" +
                    $"  TrackingId = {notificationOutcome.TrackingId}\r\n" +
                    $"  State = {notificationOutcome.State}\r\n" +
                    $"  Results (Success = {notificationOutcome.Success}, Failure = {notificationOutcome.Failure})\r\n";
                if (notificationOutcome.Results != null)
                {
                    foreach (RegistrationResult result in notificationOutcome.Results)
                    {
                        debugLog += $"    ApplicationPlatform:{result.ApplicationPlatform}, RegistrationId:{result.RegistrationId}, Outcome:{result.Outcome}\r\n";
                    }
                }
                _traceWriter.Info(debugLog);
            }
        }

        public Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(0);
        }
    }
}
