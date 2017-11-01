// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.WebJobs.Extensions.Bindings;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    internal class CosmosDBTriggerValueBinder : ValueBinder
    {
        private readonly Type _type;
        private readonly object _value;
        private readonly string _invokeString;

        public CosmosDBTriggerValueBinder(Type type, IReadOnlyList<Document> value)
                    : base(type)
        {
            if (!type.Equals(typeof(IReadOnlyList<Document>)) && !type.Equals(typeof(JArray)) && !type.Equals(typeof(string)))
            {
                throw new ArgumentException("Binding can only be done with IReadOnlyList<Document> or JArray", "type");
            }

            _value = value;
            _type = type;
            _invokeString = string.Format(CosmosDBTriggerConstants.InvokeString, value?.Count);
        }

        public override Task<object> GetValueAsync()
        {
            return Task.FromResult(GetValue());
        }

        public object GetValue()
        {
            if (_type.Equals(typeof(IReadOnlyList<Document>)))
            {
                return _value;
            }

            JArray json = JArray.FromObject(_value);

            if (_type.Equals(typeof(string)))
            {
                return json.ToString();
            }

            return json;
        }

        public override string ToInvokeString()
        {
            return _invokeString;
        }
    }
}
