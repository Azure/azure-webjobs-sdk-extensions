// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.NotificationHubs
{
    internal interface INotificationHubClientServiceFactory
    {
        INotificationHubClientService CreateService(string connectionString, string hubName, bool enableTestSend);
    }
}
