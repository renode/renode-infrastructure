//
// Copyright (c) 2010-2017 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Antmicro.Renode.UserInterface;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.UART;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Backends.Video;
using Antmicro.Renode.Extensions.Analyzers.Video;
using Antmicro.Renode.UI;

namespace Antmicro.Renode.SystemTests
{
    [TestFixture]
    public class Demos
    {
        public virtual void InitializePluginManager()
        {
            TypeManager.Instance.PluginManager.Init("CLI");
            TypeManager.Instance.PluginManager.DisableAllPlugins();
        }

        [TestFixtureSetUp]
        public void FixtureInit()
        {
            // we must create xwtProvider and register surrogate for monitor
            // before initializng plugin manager, as there might be
            // plugins in configuration that will start at this moment
            // (so the environment must be already prepared)
            new System.Threading.Thread(Emulator.ExecuteAsMainThread) { IsBackground = true }.Start();
            xwtProvider = new XwtProvider(new WindowedUserInterfaceProvider());

            // this must be set before creating monitor
            ConfigurationManager.Instance.SetNonPersistent("monitor", "consume-exceptions-from-command", false);

            monitor = new Monitor { Interaction = new DummyCommandInteraction(true) };

            context = ObjectCreator.Instance.OpenContext();
            context.RegisterSurrogate(typeof(Monitor), monitor);

            InitializePluginManager();

            TemporaryFilesManager.Instance.OnFileCreated += HandleTemporaryFileCreated;
            EmulationManager.Instance.EmulationChanged += RemovedTemporaryFiles;
            loadedFiles = new List<string>();
        }

        [TestFixtureTearDown]
        public void FixtureTearDown()
        {
            context.Dispose();

            EmulationManager.RebuildInstance();
            xwtProvider.Dispose();
            TemporaryFilesManager.Instance.OnFileCreated -= HandleTemporaryFileCreated;
            EmulationManager.Instance.EmulationChanged -= RemovedTemporaryFiles;
        }

        [Test, TestCaseSource("GetDemos")]
        public void ShouldLoadScript(string demo)
        {
            try
            {
                Logger.Dispose();

                EmulationManager.Instance.CurrentEmulation.MachineAdded += machine =>
                {
                    machine.StateChanged += (changedMachine, stateArgs) =>
                    {
                        if(stateArgs.CurrentState == MachineStateChangedEventArgs.State.Disposed)
                        {
                            foreach(var peripheral in changedMachine.GetPeripheralsOfType<IPeripheral>())
                            {
                                EmulationManager.Instance.CurrentEmulation.BackendManager.HideAnalyzersFor(peripheral);
                            }
                        }
                    };
                };
                Assert.IsTrue(monitor.TryExecuteScript(demo), "Failed to load script {0}", demo);
                Assert.IsFalse(((DummyCommandInteraction)monitor.Interaction).ErrorDetected);
            }
            catch(ObjectDisposedException)
            {
                //swallow this exception
            }
        }

        [SetUp]
        public void TestSetup()
        {
            EmulationManager.Instance.CurrentEmulation.BackendManager.SetPreferredAnalyzer(typeof(UARTBackend), typeof(DummyUartAnalyzer));
            EmulationManager.Instance.CurrentEmulation.BackendManager.SetPreferredAnalyzer(typeof(VideoBackend), typeof(DummyVideoAnalyzer));
        }

        [TearDown]
        public void TestClear()
        {
            EmulationManager.Instance.Clear();
            TypeManager.Instance.PluginManager.DisableAllPlugins();
            Logger.Dispose();
        }       

        private static string[] GetDemos()
        {
            if(!Misc.TryGetRootDirectory(out var rootDirectory))
            {
                throw new ArgumentException("Couldn't get root directory.");
            }
            var provider = new DemosParser(Path.Combine(rootDirectory, DemosParser.DemosPath));
            var demos = provider.Demos.Select(x => x.Path);

            return demos.ToArray();
        }

        private void HandleTemporaryFileCreated(string path)
        {
            loadedFiles.Add(path);
        }

        private void RemovedTemporaryFiles()
        {
            foreach(var file in loadedFiles)
            {
                File.Delete(file);
            }
        }

        private List<string> loadedFiles;
        private Monitor monitor;
        private XwtProvider xwtProvider;
        private ObjectCreator.Context context;

        [HideInMonitor]
        private class DummyUartAnalyzer : BasicPeripheralBackendAnalyzer<UARTBackend>
        {
            public override void Show()
            {
                // this is intentionally left blank
            }

            public override void Hide()
            {
                // this is intentionally left blank
            }
        }

        [HideInMonitor]
        private class DummyVideoAnalyzer : VideoAnalyzer
        {
            public override void Show()
            {
                // this is intentionally left blank
            }

            public override void Hide()
            {
                // this is intentionally left blank
            }
        }
    }
}
