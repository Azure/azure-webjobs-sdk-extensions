// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Microsoft.Azure.Documents;
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
            
            if (!(resolvedAttribute is CosmosDBAttribute cosmosDBAttribute))
            {
                throw new NotSupportedException($"This policy is only supported for {nameof(CosmosDBAttribute)}.");
            }

            // build a SqlParameterCollection for each parameter            
            SqlParameterCollection paramCollection = new SqlParameterCollection();

            string bindingTemplatePattern = bindingTemplate.Pattern;
            
            IDictionary<string, string> expandedTokens = GetExpandedTokens(bindingTemplate, bindingData);
            foreach (var token in expandedTokens)
            {
                string bindingExpression = $"{{{token.Key}}}";
                if (bindingTemplatePattern.Contains(bindingExpression))
                {
                    string sqlTokenName = $"@{EscapeSqlParameterName(token.Key)}";
                    paramCollection.Add(new SqlParameter(sqlTokenName, token.Value));
                    bindingTemplatePattern = bindingTemplatePattern.Replace($"{{{token.Key}}}", sqlTokenName);
                }
            }

            cosmosDBAttribute.SqlQueryParameters = paramCollection;

            return bindingTemplatePattern;
        }

        private IDictionary<string, string> GetExpandedTokens(BindingTemplate bindingTemplate, IReadOnlyDictionary<string, object> bindingData)
        {
            var expandedTokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var tokenName in bindingTemplate.ParameterNames.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (bindingData[tokenName] is string tokenValue)
                {
                    expandedTokens.Add(tokenName, tokenValue);
                }
                else if (bindingData[tokenName] is IDictionary<string, string> tokenDictionary)
                {
                    foreach (var item in tokenDictionary)
                    {
                        expandedTokens.Add($"{tokenName}.{item.Key}", item.Value);
                    }
                }
                else
                {
                    throw new ArgumentException($"{tokenName} is an invalid type.");
                }
            }
            return expandedTokens;
        }

        private string EscapeSqlParameterName(string name)
        {
            const string escapeChar = "_";
            return name.Replace(".", escapeChar).Replace("-", escapeChar);
        }
    }
}
