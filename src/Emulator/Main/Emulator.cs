//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Threading;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.UserInterface;
using System.Collections.Concurrent;
using Antmicro.Renode.Utilities;
using System.IO;

namespace Antmicro.Renode
{
    public static class Emulator
    {
        static Emulator()
        {
            UserDirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".renode");
        }

        public static void Exit()
        {
            var beforeExit = BeforeExit;
            if(beforeExit != null)
            {
                beforeExit();
            }
            exitEvent.Set();
        }

        public static void WaitForExit()
        {
            exitEvent.Wait();
        }

        public static void DisposeAll()
        {
            EmulationManager.Instance.Clear();
            TypeManager.Instance.Dispose();
            Logger.Dispose();
        }

        public static IUserInterfaceProvider UserInterfaceProvider
        {
            get
            {
                if(userInterfaceProvider == null)
                {
                    throw new RecoverableException("User interface provider not set");
                }
                return userInterfaceProvider;
            }
            set
            {
                userInterfaceProvider = value;
            }
        }

        public static string UserDirectoryPath
        {
            get
            {
                return userDirectoryPath;
            }

            set
            {
                userDirectoryPath = value;
                Directory.CreateDirectory(userDirectoryPath);
            }
        }

        private static string userDirectoryPath;

        public static void ExecuteOnMainThread(Action what)
        {
            actionsOnMainThread.Add(what);
        }

        public static void ExecuteAsMainThread()
        {
            Action action;
            while(actionsOnMainThread.TryTake(out action, -1))
            {
                action();
            }
        }

        public static void FinishExecutionAsMainThread()
        {
            actionsOnMainThread.CompleteAdding();
        }

        private static readonly BlockingCollection<Action> actionsOnMainThread = new BlockingCollection<Action>();

        public static event Action BeforeExit;
        private static IUserInterfaceProvider userInterfaceProvider;
        private static ManualResetEventSlim exitEvent = new ManualResetEventSlim();
    }
}

