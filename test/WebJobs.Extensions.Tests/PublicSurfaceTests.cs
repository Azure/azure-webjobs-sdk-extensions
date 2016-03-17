// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests
{
    public class PublicSurfaceTests
    {
        [Fact]
        public void ExtensionsPublicSurface_LimitedToSpecificTypes()
        {
            var assembly = typeof(TimerTriggerAttribute).Assembly;

            var expected = new[]
            {
                "ConstantSchedule",
                "CoreJobHostConfigurationExtensions",
                "CronSchedule",
                "DailySchedule",
                "ErrorTriggerAttribute",
                "ExecutionContext",
                "FileAttribute",
                "FileProcessor",
                "FileProcessorFactoryContext",
                "FilesConfiguration",
                "FilesJobHostConfigurationExtensions",
                "FileSystemScheduleMonitor",
                "StorageScheduleMonitor",
                "FileTriggerAttribute",
                "IFileProcessorFactory",
                "JobHostConfigurationExtensions",
                "ScheduleMonitor",
                "ScheduleStatus",
                "SlidingWindowTraceFilter",
                "StreamValueBinder",
                "TimerInfo",
                "TimerJobHostConfigurationExtensions",
                "TimerSchedule",
                "TimersConfiguration",
                "TimerTriggerAttribute",
                "TraceFilter",
                "TraceMonitor",
                "ValueBinder",
                "WeeklySchedule"
            };

            AssertPublicTypes(expected, assembly);
        }

        [Fact]
        public void SendGridPublicSurface_LimitedToSpecificTypes()
        {
            var assembly = typeof(SendGridAttribute).Assembly;

            var expected = new[]
            {
                "SendGridAttribute",
                "SendGridConfiguration",
                "SampleJobHostConfigurationExtensions"
            };

            AssertPublicTypes(expected, assembly);
        }

        [Fact]
        public void WebHooksPublicSurface_LimitedToSpecificTypes()
        {
            var assembly = typeof(WebHookTriggerAttribute).Assembly;

            var expected = new[]
            {
                "WebHookTriggerAttribute",
                "WebHooksConfiguration",
                "WebHooksJobHostConfigurationExtensions",
                "WebHookContext"
            };

            AssertPublicTypes(expected, assembly);
        }

        [Fact]
        public void NotificationHubsPublicSurface_LimitedToSpecificTypes()
        {
            var assembly = typeof(NotificationHubAttribute).Assembly;

            var expected = new[]
            {
                "NotificationHubAttribute",
                "NotificationHubConfiguration",
                "NotificationHubJobHostConfigurationExtensions",
            };

            AssertPublicTypes(expected, assembly);
        }

        private static List<string> GetAssemblyReferences(Assembly assembly)
        {
            var assemblyRefs = assembly.GetReferencedAssemblies();
            var names = (from assemblyRef in assemblyRefs
                         orderby assemblyRef.Name.ToUpperInvariant()
                         select assemblyRef.Name).ToList();
            return names;
        }

        private static void AssertPublicTypes(IEnumerable<string> expected, Assembly assembly)
        {
            var publicTypes = assembly.GetExportedTypes()
                .Select(type => type.Name)
                .OrderBy(n => n);

            AssertPublicTypes(expected.ToArray(), publicTypes.ToArray());
        }

        private static void AssertPublicTypes(string[] expected, string[] actual)
        {
            var newlyIntroducedPublicTypes = actual.Except(expected).ToArray();

            if (newlyIntroducedPublicTypes.Length > 0)
            {
                string message = String.Format("Found {0} unexpected public type{1}: \r\n{2}",
                    newlyIntroducedPublicTypes.Length,
                    newlyIntroducedPublicTypes.Length == 1 ? string.Empty : "s",
                    string.Join("\r\n", newlyIntroducedPublicTypes));
                Assert.True(false, message);
            }

            var missingPublicTypes = expected.Except(actual).ToArray();

            if (missingPublicTypes.Length > 0)
            {
                string message = String.Format("missing {0} public type{1}: \r\n{2}",
                    missingPublicTypes.Length,
                    missingPublicTypes.Length == 1 ? string.Empty : "s",
                    string.Join("\r\n", missingPublicTypes));
                Assert.True(false, message);
            }
        }
    }
}
