// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.WindowsAzure.MobileServices;

namespace Microsoft.Azure.WebJobs.Extensions.MobileApps.Bindings
{
    internal class MobileTableJObjectTableBuilder : IConverter<MobileTableAttribute, IMobileServiceTable>
    {
        private MobileAppsExtensionConfigProvider _configProvider;

        public MobileTableJObjectTableBuilder(MobileAppsExtensionConfigProvider configProvider)
        {
            _configProvider = configProvider;
        }

        public IMobileServiceTable Convert(MobileTableAttribute attribute)
        {
            MobileTableContext context = _configProvider.CreateContext(attribute);
            IMobileServiceTable table = context.Client.GetTable(context.ResolvedAttribute.TableName);
            return table;
        }
    }
}
