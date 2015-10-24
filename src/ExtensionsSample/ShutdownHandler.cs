// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Runtime.InteropServices;

namespace ExtensionsSample
{
    public static class ShutdownHandler
    {
        private static ConsoleEventDelegate eventHandler = new ConsoleEventDelegate(ConsoleEventCallback);
        private static Action action;
        private delegate bool ConsoleEventDelegate(int eventType);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate callback, bool add);

        public static void Register(Action a)
        {
            SetConsoleCtrlHandler(eventHandler, true);
            action = a;
        }

        private static bool ConsoleEventCallback(int eventType)
        {
            if (eventType == 2)
            {
                action();
            }
            return false;
        }
    }
}
