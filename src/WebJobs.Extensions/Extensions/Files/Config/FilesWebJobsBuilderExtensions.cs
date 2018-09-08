// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Extensions.Files;
using Microsoft.Azure.WebJobs.Extensions.Files;
using Microsoft.Azure.WebJobs.Extensions.Files.Listener;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting
{
    /// <summary>
    /// Extension methods for Files integration
    /// </summary>
    public static class FilesWebJobsBuilderExtensions
    {
        /// <summary>
        /// Adds the Files extension to the provided <see cref="IWebJobsBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> to configure.</param>
        public static IWebJobsBuilder AddFiles(this IWebJobsBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.AddExtension<FilesExtensionConfigProvider>()
                .BindOptions<FilesOptions>();
            builder.Services.AddSingleton<IFileProcessorFactory, DefaultFileProcessorFactory>();

            return builder;
        }

        /// <summary>
        /// Adds the Files extension to the provided <see cref="IWebJobsBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> to configure.</param>
        /// <param name="configure">An <see cref="Action{FilesOptions}"/> to configure the provided <see cref="FilesOptions"/>.</param>
        public static IWebJobsBuilder AddFiles(this IWebJobsBuilder builder, Action<FilesOptions> configure)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            builder.AddFiles();
            builder.Services.Configure(configure);

            return builder;
        }
    }
}
