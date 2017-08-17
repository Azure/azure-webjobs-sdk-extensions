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

namespace Microsoft.Azure.WebJobs.Extensions.DocumentDB
{
    internal class DocumentDBSqlResolutionPolicy : IResolutionPolicy
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

            DocumentDBAttribute docDbAttribute = resolvedAttribute as DocumentDBAttribute;
            if (docDbAttribute == null)
            {
                throw new NotSupportedException($"This policy is only supported for {nameof(DocumentDBAttribute)}.");
            }

            // build a SqlParameterCollection for each parameter            
            SqlParameterCollection paramCollection = new SqlParameterCollection();
            // also build up a dictionary replacing '{token}' with '@token' 
            IDictionary<string, object> replacements = new Dictionary<string, object>();
            foreach (var token in bindingTemplate.ParameterNames.Distinct())
            {
                // Is bindingData string, or dictionary
                Dictionary<string, string> dict = bindingData[token] as Dictionary<string, string>;
                if (dict != null)
                {
                    // Map all the end values into the parameter collection removing dots from sql parameter values
                    var replacementObj = new Dictionary<string, string>();
                    foreach (var item in dict)
                    {
                        string sqlTokenItem = $"@{token}{item.Key}";
                        paramCollection.Add(new SqlParameter(sqlTokenItem, dict[item.Key]));
                        replacementObj.Add(item.Key, sqlTokenItem);
                    }

                    replacements.Add(token, replacementObj);

                    continue;
                }

                string sqlToken = $"@{token}";
                paramCollection.Add(new SqlParameter(sqlToken, bindingData[token]));
                replacements.Add(token, sqlToken);
            }

            docDbAttribute.SqlQueryParameters = paramCollection;

            string replacement = bindingTemplate.Bind(new ReadOnlyDictionary<string, object>(replacements));
            return replacement;
        }
    }
}
