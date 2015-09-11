// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Extensions.WebHooks;
using Microsoft.Azure.WebJobs.Host.Config;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Extension class used to register WebHook extensions.
    /// </summary>
    public static class WebHooksJobHostConfigurationExtensions
    {
        /// <summary>
        /// Enables use of the WebHooks extensions.
        /// </summary>
        /// <remarks>
        /// <para>
        /// In addition to enabling HTTP POST invocation of functions decorated with <see cref="WebHookTriggerAttribute"/>
        /// this also enables HTTP invocation of other functions as well. For functions not decorated with
        /// <see cref="WebHookTriggerAttribute"/>, they can be invoked via an implicit route of the form
        /// {TypeName}/{FunctionName}. The body should be a valid json string representing the data that you would
        /// pass in to <see cref="JobHost.Call(System.Reflection.MethodInfo, object)"/>.
        /// </para>
        /// <para>
        /// Authentication of incoming requests is handled outside of this extension. When running under the normal
        /// Azure Web App host, the extension will be listening on a loopback port that the SCM host has opened for
        /// the job, and SCM forwards authenticated requests through (SCM credentials are required to invoke the SCM endpoints).
        /// </para>
        /// </remarks>
        /// <param name="config">The <see cref="JobHostConfiguration"/> to configure.</param>
        /// <param name="webHooksConfig">The <see cref="WebHooksConfiguration"/> to use.</param>
        public static void UseWebHooks(this JobHostConfiguration config, WebHooksConfiguration webHooksConfig = null)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            if (webHooksConfig == null)
            {
                webHooksConfig = new WebHooksConfiguration();
            }

            config.RegisterExtensionConfigProvider(new WebHooksExtensionConfig(webHooksConfig));
        }

        private class WebHooksExtensionConfig : IExtensionConfigProvider
        {
            private readonly WebHooksConfiguration _webHooksConfig;

            public WebHooksExtensionConfig(WebHooksConfiguration webHooksConfig)
            {
                _webHooksConfig = webHooksConfig;
            }

            public void Initialize(ExtensionConfigContext context)
            {
                if (context == null)
                {
                    throw new ArgumentNullException("context");
                }

                WebHookDispatcher dispatcher = new WebHookDispatcher(_webHooksConfig, context.Host, context.Config, context.Trace);
                context.Config.RegisterBindingExtension(new WebHookTriggerAttributeBindingProvider(dispatcher));
            }
        }
    }
}
