// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Common
{
    public class TestNameResolver : INameResolver
    {
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>();

        public Dictionary<string, string> Values
        {
            get
            {
                return _values;
            }
        }

        public string Resolve(string name)
        {
            string value;

            Values.TryGetValue(name, out value);

            return value;
        }
    }
}
