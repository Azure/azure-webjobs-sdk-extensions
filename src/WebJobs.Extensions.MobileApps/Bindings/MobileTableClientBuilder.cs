// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.WindowsAzure.MobileServices;

namespace Microsoft.Azure.WebJobs.Extensions.MobileApps.Bindings
{
    internal class MobileTableClientBuilder : IConverter<MobileTableAttribute, IMobileServiceClient>
    {
        private MobileAppsConfiguration _config;

        public MobileTableClientBuilder(MobileAppsConfiguration config)
        {
            _config = config;
        }

        public IMobileServiceClient Convert(MobileTableAttribute attribute)
        {
            MobileTableContext context = _config.CreateContext(attribute);
            return context.Client;
        }
    }
}
