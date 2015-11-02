// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Threading;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Common
{
    public abstract class CultureFixture : IDisposable
    {
        private readonly CultureInfo _originalCultureInfo;

        protected CultureFixture(string culture)
        {
            _originalCultureInfo = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = string.IsNullOrEmpty(culture) ? 
                CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(culture);
        }

        public void Dispose()
        {
            Thread.CurrentThread.CurrentCulture = _originalCultureInfo;
        }

        public class Invariant : CultureFixture
        {
            public Invariant() : base(null)
            {
            }
        }

        public class EnUs : CultureFixture
        {
            public EnUs() : base("en-Us")
            {
            }
        }
    }
}
