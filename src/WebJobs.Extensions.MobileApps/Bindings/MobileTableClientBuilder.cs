// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.WindowsAzure.MobileServices;

namespace Microsoft.Azure.WebJobs.Extensions.MobileApps.Bindings
{
    internal class MobileTableClientBuilder : IConverter<MobileTableAttribute, IMobileServiceClient>
    {
        private MobileAppsExtensionConfigProvider _configProvider;

        public MobileTableClientBuilder(MobileAppsExtensionConfigProvider configProvider)
        {
            _configProvider = configProvider;
        }

        public IMobileServiceClient Convert(MobileTableAttribute attribute)
        {
            MobileTableContext context = _configProvider.CreateContext(attribute);
            return context.Client;
        }
    }
}
