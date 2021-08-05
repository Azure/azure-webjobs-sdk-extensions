// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.Logging
{
    public static class ScriptLoggingBuilderExtensions
    {
        //TODO(gzuber): move
        public static readonly ImmutableArray<string> ScriptConstantsSystemLogCategoryPrefixes = ImmutableArray.Create("Microsoft.Azure.WebJobs.", "Function.", "Worker.", "Host.");
        public static readonly string ConfigurationSectionNamesJobHost = "AzureFunctionsJobHost";

        private static ConcurrentDictionary<string, bool> _filteredCategoryCache = new ConcurrentDictionary<string, bool>();

        public static ILoggingBuilder AddDefaultWebJobsFiltersAIExtension(this ILoggingBuilder builder)
        {
            builder.SetMinimumLevel(LogLevel.None);
            builder.AddFilter((c, l) => Filter(c, l, LogLevel.Information));
            return builder;
        }

        public static ILoggingBuilder AddDefaultWebJobsFiltersAIExtension<T>(this ILoggingBuilder builder, LogLevel level) where T : ILoggerProvider
        {
            builder.AddFilter<T>(null, LogLevel.None);
            builder.AddFilter<T>((c, l) => Filter(c, l, level));
            return builder;
        }

        internal static bool Filter(string category, LogLevel actualLevel, LogLevel minLevel)
        {
            return actualLevel >= minLevel && IsFiltered(category);
        }

        private static bool IsFiltered(string category)
        {
            return _filteredCategoryCache.GetOrAdd(category, c => ScriptConstantsSystemLogCategoryPrefixes.Where(p => category.StartsWith(p)).Any());
        }

        public static void AddConsoleIfEnabledAIExtension(this ILoggingBuilder builder, HostBuilderContext context)
        {
            AddConsoleIfEnabled(builder, context.HostingEnvironment.IsDevelopment(), context.Configuration);
        }

        public static void AddConsoleIfEnabledAIExtension(this ILoggingBuilder builder, WebHostBuilderContext context)
        {
            AddConsoleIfEnabled(builder, context.HostingEnvironment.IsDevelopment(), context.Configuration);
        }

        private static void AddConsoleIfEnabled(ILoggingBuilder builder, bool isDevelopment, IConfiguration configuration)
        {
            // console logging defaults to false, except for self host
            bool enableConsole = isDevelopment;

            string consolePath = ConfigurationPath.Combine(ConfigurationSectionNamesJobHost, "Logging", "Console", "IsEnabled");
            IConfigurationSection configSection = configuration.GetSection(consolePath);

            if (configSection.Exists())
            {
                // if it has been explicitly configured that value overrides default
                enableConsole = configSection.Get<bool>();
            }

            if (enableConsole)
            {
                builder.AddConsole();
            }
        }
    }
}
