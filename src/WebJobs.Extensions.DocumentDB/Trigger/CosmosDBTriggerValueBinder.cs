// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DocumentDB
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.WebJobs.Extensions.Bindings;
    using Newtonsoft.Json.Linq;

    internal class CosmosDBTriggerValueBinder : ValueBinder
    {
        private readonly Type _type;
        private readonly object _value;
        private readonly string _invokeString;

        public CosmosDBTriggerValueBinder(Type type, IReadOnlyList<Document> value)
                    : base(type)
        {
            if (!type.Equals(typeof(IReadOnlyList<Document>)) && !type.Equals(typeof(JArray)))
            {
                throw new ArgumentException("Binding can only be done with IReadOnlyList<Document> or JArray", "type");
            }

            _value = value;
            _type = type;
            _invokeString = string.Format(CosmosDBTriggerConstants.InvokeString, value?.Count);
        }

        public override Task<object> GetValueAsync()
        {
            if (_type.Equals(typeof(IReadOnlyList<Document>)))
            {
                return Task.FromResult(_value);
            }

            return Task.FromResult((object)JArray.FromObject(_value));
        }

        public object GetValue()
        {
            if (_type.Equals(typeof(IReadOnlyList<Document>)))
            {
                return _value;
            }

            return JArray.FromObject(_value);
        }

        public override string ToInvokeString()
        {
            return _invokeString;
        }
    }
}
