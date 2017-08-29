// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.WindowsAzure.MobileServices;

namespace Microsoft.Azure.WebJobs.Extensions.MobileApps.Bindings
{
    internal class MobileTableJObjectTableBuilder : IConverter<MobileTableAttribute, IMobileServiceTable>
    {
        private MobileAppsConfiguration _config;

        public MobileTableJObjectTableBuilder(MobileAppsConfiguration config)
        {
            _config = config;
        }

        public IMobileServiceTable Convert(MobileTableAttribute attribute)
        {
            MobileTableContext context = _config.CreateContext(attribute);
            IMobileServiceTable table = context.Client.GetTable(context.ResolvedAttribute.TableName);
            return table;
        }
    }
}
