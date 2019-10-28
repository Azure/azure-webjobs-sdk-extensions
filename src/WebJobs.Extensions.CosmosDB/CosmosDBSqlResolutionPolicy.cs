// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Bindings.Path;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    internal class CosmosDBSqlResolutionPolicy : IResolutionPolicy
    {
        public string TemplateBind(PropertyInfo propInfo, Attribute resolvedAttribute, BindingTemplate bindingTemplate, IReadOnlyDictionary<string, object> bindingData)
        {
            if (bindingTemplate == null)
            {
                throw new ArgumentNullException(nameof(bindingTemplate));
            }

            if (bindingData == null)
            {
                throw new ArgumentNullException(nameof(bindingData));
            }

            CosmosDBAttribute docDbAttribute = resolvedAttribute as CosmosDBAttribute;
            if (docDbAttribute == null)
            {
                throw new NotSupportedException($"This policy is only supported for {nameof(CosmosDBAttribute)}.");
            }

            // also build up a dictionary replacing '{token}' with '@token' 
            IDictionary<string, string> replacements = new Dictionary<string, string>();
            List<(string, object)> parameters = new List<(string, object)>();
            foreach (var token in bindingTemplate.ParameterNames.Distinct())
            {
                string sqlToken = $"@{token}";
                parameters.Add((sqlToken, bindingData[token]));
                replacements.Add(token, sqlToken);
            }

            docDbAttribute.SqlQueryParameters = parameters;

            return bindingTemplate.Bind(new ReadOnlyDictionary<string, string>(replacements));
        }
    }
}
