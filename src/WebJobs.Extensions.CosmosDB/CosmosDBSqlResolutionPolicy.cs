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

            // also build up a dictionary replacing '{token}' with '@token' 
            IDictionary<string, object> replacements = new Dictionary<string, object>();
            foreach (var token in bindingTemplate.ParameterNames.Distinct())
            {
                object tokenObject = bindingData[token];
                if (tokenObject is string tokenString)
                {
                    string sqlToken = GetSqlParameterName(token);
                    paramCollection.Add(new SqlParameter(sqlToken, tokenString));
                    replacements.Add(token, sqlToken);
                }
                else if (tokenObject is IDictionary<string, string> tokenDictionary)
                {
                    IDictionary<string, string> tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var item in tokenDictionary)
                    {
                        string fullToken = $"{token}.{item.Key}";
                        bool tokenInUse = bindingTemplate.Pattern.Contains(fullToken);
                        if (tokenInUse)
                        {
                            string sqlToken = GetSqlParameterName(fullToken);
                            paramCollection.Add(new SqlParameter(sqlToken, item.Value));
                            tokens.Add(item.Key, sqlToken);
                        }
                    }
                    replacements.Add(token, tokens);
                }
            }

            cosmosDBAttribute.SqlQueryParameters = paramCollection;

            string replacement = bindingTemplate.Bind(new ReadOnlyDictionary<string, object>(replacements));
            return replacement;
        }

        private string GetSqlParameterName(string name)
        {
            const string safeSeparator = "_";
            return "@" + name.Replace(".", safeSeparator).Replace("-", safeSeparator);
        }
    }
}