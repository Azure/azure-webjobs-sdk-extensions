// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.Files
{
    public class FileAttributeTests
    {
        [Theory]
        [InlineData("foo/bar", @"foo\bar")]
        [InlineData(@"foo\bar", @"foo\bar")]
        [InlineData(@"foo/bar\baz/", @"foo\bar\baz\")]
        [InlineData("foobar", "foobar")]
        public void Path_SeparatorsAreNormalized(string path, string expected)
        {
            FileTriggerAttribute attribute = new FileTriggerAttribute(path, "*.xml");
            Assert.Equal(expected, attribute.Path);
        }

        [Theory]
        [InlineData(@"foo\bar", @"foo\bar")]
        [InlineData(@"foo\bar\", @"foo\bar\")]
        [InlineData(@"foo\bar\{name}", @"foo\bar")]
        public void GetRootPath_RemovesTemplate(string path, string expected)
        {
            FileTriggerAttribute attribute = new FileTriggerAttribute(path, "*.xml");
            Assert.Equal(expected, attribute.GetRootPath());
        }
    }
}
