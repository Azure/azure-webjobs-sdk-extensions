using Microsoft.Azure.WebJobs.Extensions.Http;
using System;
using System.Collections.Generic;
using System.Text;
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
