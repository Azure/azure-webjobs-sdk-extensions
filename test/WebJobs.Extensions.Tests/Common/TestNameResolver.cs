// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Common
{
    public class TestNameResolver : INameResolver
    {
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>();
        private bool _throwException;

        public TestNameResolver(bool throwNotImplementedException = false)
        {
            // DefaultNameResolver throws so this helps simulate that for testing
            _throwException = throwNotImplementedException;
        }

        public Dictionary<string, string> Values
        {
            get
            {
                return _values;
            }
        }

        public string Resolve(string name)
        {
            if (_throwException)
            {
                throw new NotImplementedException("INameResolver must be supplied to resolve '%" + name + "%'.");
            }

            string value = null;
            Values.TryGetValue(name, out value);
            return value;
        }
    }
}
