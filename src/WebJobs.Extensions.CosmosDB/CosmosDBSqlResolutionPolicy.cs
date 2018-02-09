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
                    AddParameter(paramCollection, replacements, tokenString, token);
                }
                else if (tokenObject is IDictionary<string, string> tokenDictionary)
                {
                    IDictionary<string, object> tokens = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    foreach (var item in tokenDictionary)
                    {
                        if (IsTokenInUse(bindingTemplate, token, item.Key))
                        {
                            AddParameter(paramCollection, tokens, item.Value, token, item.Key);
                        }
                    }
                    replacements.Add(token, tokens);
                }
            }

            cosmosDBAttribute.SqlQueryParameters = paramCollection;

            string replacement = bindingTemplate.Bind(new ReadOnlyDictionary<string, object>(replacements));
            return replacement;
        }

        private bool IsTokenInUse(BindingTemplate bindingTemplate, string firstTokenNameSegment, string secondTokenNameSegment)
        {
            string fullToken = GetFullTokenName(firstTokenNameSegment, secondTokenNameSegment);
            return bindingTemplate.Pattern.Contains(fullToken);
        }

        private void AddParameter(SqlParameterCollection paramCollection, IDictionary<string, object> tokens, object sqlParamValue,
            string firstTokenNameSegment, string secondTokenNameSegment = null)
        {
            string fullTokenName = GetFullTokenName(firstTokenNameSegment, secondTokenNameSegment);
            string tokenName = secondTokenNameSegment ?? firstTokenNameSegment;

            bool needsEscaping = !string.IsNullOrEmpty(secondTokenNameSegment);
            string sqlToken = "@" + (needsEscaping ? EscapeSqlParameterName(fullTokenName) : fullTokenName);

            paramCollection.Add(new SqlParameter(sqlToken, sqlParamValue));
            tokens.Add(tokenName, sqlToken);
        }

        private string GetFullTokenName(string firstTokenNameSegment, string secondTokenNameSegment)
        {
            return string.IsNullOrEmpty(secondTokenNameSegment) ?
                firstTokenNameSegment :
                $"{firstTokenNameSegment}.{secondTokenNameSegment}";
        }

        private string EscapeSqlParameterName(string name)
        {
            return Guid.NewGuid().ToString("N");
        }
    }
}