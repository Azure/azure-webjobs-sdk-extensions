﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Globalization;

namespace Microsoft.Azure.WebJobs.Extensions.Timers
{
    /// <summary>
    /// A simple constant interval scheduling strategy.
    /// </summary>
    public class ConstantSchedule : TimerSchedule
    {
        private readonly TimeSpan _interval;
        private TimeSpan? _intervalOverride;

        /// <summary>
        /// Constructs an instance using the specified interval.
        /// </summary>
        /// <param name="interval">The constant interval between schedule occurrences.</param>
        public ConstantSchedule(TimeSpan interval)
        {
            _interval = interval;
        }

        /// <inheritdoc/>
        // We always want to run these based on the configured interval. We don't want to adjust
        // based on whether the next time falls across a DST boundary.
        public override bool AdjustForDST => false;

        /// <inheritdoc/>
        public override DateTime GetNextOccurrence(DateTime now)
        {
            TimeSpan nextInterval = _interval;
            if (_intervalOverride != null)
            {
                nextInterval = _intervalOverride.Value;
                _intervalOverride = null;
            }

            return now + nextInterval;
        }

        /// <summary>
        /// Override the next schedule interval using the specified interval.
        /// </summary>
        /// <param name="interval">The one time interval to use for the next occurrence.</param>
        public void SetNextInterval(TimeSpan interval)
        {
            _intervalOverride = interval;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "Constant: {0}", _interval.ToString());
        }
    }
}
