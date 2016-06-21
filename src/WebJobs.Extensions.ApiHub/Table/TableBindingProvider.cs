// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Bindings.Path;

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

            ValidateContract(attribute, ConfigContext.NameResolver, context.BindingDataContract);

            return Task.FromResult<IBinding>(binding);
        }

        private static void ValidateContract(ApiHubTableAttribute attribute, INameResolver nameResolver, IReadOnlyDictionary<string, Type> contract)
        {
            ValidateContract(attribute.DataSetName, nameResolver, contract);
            ValidateContract(attribute.TableName, nameResolver, contract);
            ValidateContract(attribute.EntityId, nameResolver, contract);
        }

        private static void ValidateContract(string value, INameResolver nameResolver, IReadOnlyDictionary<string, Type> contract)
        {
            if (!string.IsNullOrEmpty(value))
            {
                if (nameResolver != null)
                {
                    value = nameResolver.ResolveWholeString(value);
                }
                BindingTemplate bindingTemplate = BindingTemplate.FromString(value, ignoreCase: true);
                bindingTemplate.ValidateContractCompatibility(contract);
            }
        }
    }
}
