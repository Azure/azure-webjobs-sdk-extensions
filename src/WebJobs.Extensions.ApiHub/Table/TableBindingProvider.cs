// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;

namespace Microsoft.Azure.WebJobs.Extensions.ApiHub.Table
{
    internal class TableBindingProvider : IBindingProvider
    {
        public TableBindingProvider(TableConfigContext configContext)
        {
            ConfigContext = configContext;
        }

        private TableConfigContext ConfigContext { get; set; }

        public Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            var parameter = context.Parameter;
            var attribute = parameter.GetTableAttribute();
            if (attribute == null)
            {
                return Task.FromResult<IBinding>(null);
            }

            IBinding binding;
            if (string.IsNullOrEmpty(attribute.TableName))
            {
                binding = new TableClientBinding(parameter, ConfigContext);
            }
            else if (string.IsNullOrEmpty(attribute.EntityId))
            {
                binding = new TableBinding(parameter, ConfigContext);
            }
            else
            {
                binding = new TableEntityBinding(parameter, ConfigContext);
            }

            return Task.FromResult<IBinding>(binding);
        }
    }
}
