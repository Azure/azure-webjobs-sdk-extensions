// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Extensions.Http;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.Http
{
    public class HttpTriggerAttributeConstructionTests
    {
        [Fact]
        public void HttpTriggerAuthLevelCtor_NullMethods()
        {
            var trigger = new HttpTriggerAttribute(AuthorizationLevel.Admin);

            Assert.Null(trigger.Methods);
        }
    }
}
