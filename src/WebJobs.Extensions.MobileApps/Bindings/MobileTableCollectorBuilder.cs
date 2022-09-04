// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.MobileApps.Bindings
{
    internal class MobileTableCollectorBuilder<T> : IConverter<MobileTableAttribute, IAsyncCollector<T>>
    {
        private MobileAppsExtensionConfigProvider _configProvider;

        public MobileTableCollectorBuilder(MobileAppsExtensionConfigProvider configProvider)
        {
            _configProvider = configProvider;
        }

        public IAsyncCollector<T> Convert(MobileTableAttribute attribute)
        {
            MobileTableContext context = _configProvider.CreateContext(attribute);
            return new MobileTableAsyncCollector<T>(context);
        }
    }
}
