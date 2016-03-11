// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Azure.WebJobs.Extensions.EasyTables;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Extension methods for EasyTable integration.
    /// </summary>
    public static class EasyTablesHostConfigurationExtensions
    {
        /// <summary>
        /// Enables use of the EasyTable extensions.
        /// </summary>
        /// <param name="config">The <see cref="JobHostConfiguration"/> to configure.</param>
        /// <param name="easyTablesConfig">The <see cref="EasyTablesConfiguration"/> to use.</param>
        public static void UseEasyTables(this JobHostConfiguration config, EasyTablesConfiguration easyTablesConfig = null)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            if (easyTablesConfig == null)
            {
                easyTablesConfig = new EasyTablesConfiguration();
            }

            config.RegisterExtensionConfigProvider(easyTablesConfig);
        }
    }
}