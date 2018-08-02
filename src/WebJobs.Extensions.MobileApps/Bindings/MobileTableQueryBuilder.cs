// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.WindowsAzure.MobileServices;

namespace Microsoft.Azure.WebJobs.Extensions.MobileApps.Bindings
{
    internal class MobileTableQueryBuilder<T> : IConverter<MobileTableAttribute, IMobileServiceTableQuery<T>>
    {
        private MobileAppsExtensionConfigProvider _configProvider;

        public MobileTableQueryBuilder(MobileAppsExtensionConfigProvider configProvider)
        {
            _configProvider = configProvider;
        }

        public IMobileServiceTableQuery<T> Convert(MobileTableAttribute attribute)
        {
            if (attribute == null)
            {
                throw new ArgumentNullException(nameof(attribute));
            }

            // The Table POCO rule already knows how to get the table
            MobileTablePocoTableBuilder<T> tablePocoBuilder = new MobileTablePocoTableBuilder<T>(_configProvider);
            IMobileServiceTable<T> table = tablePocoBuilder.Convert(attribute);

            return table.CreateQuery();
        }
    }
}
