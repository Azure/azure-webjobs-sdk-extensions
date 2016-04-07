// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

extern alias DocumentDB;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DocumentDB::Microsoft.Azure.WebJobs.Extensions.DocumentDB;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.DocumentDB
{
    public class DocumentDBContextTests
    {
        [Fact]
        public void MaxThrottleRetries_DefaultsTo10()
        {
            // Act
            var context = new DocumentDBContext();

            // Assert
            Assert.Equal(10, context.MaxThrottleRetries);
        }
    }
}
