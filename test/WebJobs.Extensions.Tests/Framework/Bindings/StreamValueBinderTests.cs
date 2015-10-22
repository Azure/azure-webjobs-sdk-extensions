// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Azure.WebJobs.Extensions.Bindings;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests
{
    public class StreamValueBinderTests
    {
        [Fact]
        public void GetSupportedTypes_FileAccessRead_ReturnsExpectedTypes()
        {
            Type[] expected = new Type[]
            {
                typeof(Stream),
                typeof(TextReader),
                typeof(StreamReader),
                typeof(string),
                typeof(byte[])
            };

            IEnumerable<Type> result = StreamValueBinder.GetSupportedTypes(FileAccess.Read);

            Assert.True(expected.SequenceEqual(result));
        }

        [Fact]
        public void GetSupportedTypes_FileAccessWrite_ReturnsExpectedTypes()
        {
            Type[] expected = new Type[]
            {
                typeof(Stream),
                typeof(TextWriter),
                typeof(StreamWriter),
                typeof(string),
                typeof(byte[])
            };

            IEnumerable<Type> result = StreamValueBinder.GetSupportedTypes(FileAccess.Write);

            Assert.True(expected.SequenceEqual(result));
        }

        [Fact]
        public void GetSupportedTypes_FileAccessReadWrite_ReturnsExpectedTypes()
        {
            Type[] expected = new Type[]
            {
                typeof(Stream),
                typeof(TextReader),
                typeof(StreamReader),
                typeof(string),
                typeof(byte[]),
                typeof(TextWriter),
                typeof(StreamWriter)
            };

            IEnumerable<Type> result = StreamValueBinder.GetSupportedTypes(FileAccess.ReadWrite);

            Assert.True(expected.SequenceEqual(result));
        }
    }
}
