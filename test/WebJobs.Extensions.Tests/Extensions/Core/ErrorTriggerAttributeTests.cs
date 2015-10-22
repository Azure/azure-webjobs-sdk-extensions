// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Core
{
    public class ErrorTriggerAttributeTests
    {
        [Theory]
        [InlineData("")]
        [InlineData("0:blah:1")]
        [InlineData("invalid")]
        public void Constructor_ValidatesWindow(string window)
        {
            var exception = Assert.Throws<ArgumentException>(() =>
            {
                new ErrorTriggerAttribute(window, 3);
            });
            Assert.Equal("Invalid TimeSpan value specified.\r\nParameter name: window", exception.Message);
        }

        [Fact]
        public void Constructor_ValidatesThreshold()
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                new ErrorTriggerAttribute("00:10:20", -1);
            });
            Assert.Equal("threshold", exception.ParamName);
        }

        [Fact]
        public void Constructor_ValidatesFilterType()
        {
            var exception = Assert.Throws<ArgumentNullException>(() =>
            {
                Type filterType = null;
                new ErrorTriggerAttribute(filterType);
            });
            Assert.Equal("filterType", exception.ParamName);
        }

        [Theory]
        [InlineData("")]
        [InlineData("0:blah:1")]
        [InlineData("invalid")]
        public void Throttle_Setter_ValidatesTimeSpan(string throttle)
        {
            ErrorTriggerAttribute attribute = new ErrorTriggerAttribute();

            var exception = Assert.Throws<ArgumentException>(() =>
            {
                attribute.Throttle = throttle;
            });
            Assert.Equal("Invalid TimeSpan value specified.\r\nParameter name: value", exception.Message);
            Assert.Equal("value", exception.ParamName);
        }
    }
}
