// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.WebJobs;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Common
{
    public class ExplicitTypeLocator : ITypeLocator
    {
        private readonly IReadOnlyList<Type> types;

        public ExplicitTypeLocator(params Type[] types)
        {
            this.types = types.ToList().AsReadOnly();
        }

        public IReadOnlyList<Type> GetTypes()
        {
            return types;
        }
    }
}
