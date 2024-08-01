//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Migrant.Hooks;
using Antmicro.Migrant;
using System.Linq;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities.Collections;
using System.Threading;

namespace Antmicro.Renode.Core
{
    public class Emulation : IDisposable
    {
        public Emulation()
        {
            MasterTimeSource = new MasterTimeSource();
            HostMachine = new HostMachine();
            MACRepository = new MACRepository();
            ExternalsManager = new ExternalsManager();
            ExternalsManager.AddExternal(HostMachine, HostMachine.HostMachineName);
            Connector = new Connector();
            FileFetcher = new CachingFileFetcher();
            CurrentLogger = Logger.GetLogger();
            randomGenerator = new Lazy<PseudorandomNumberGenerator>(() => new PseudorandomNumberGenerator());
            nameCache = new LRUCache<object, Tuple<string, string>>(NameCacheSize);
            peripheralToMachineCache = new LRUCache<IPeripheral, IMachine>(PeripheralToMachineCacheSize);

            machs = new FastReadConcurrentTwoWayDictionary<string, IMachine>();
            machs.ItemAdded += (name, machine) =>
            {
                machine.StateChanged += OnMachineStateChanged;
                machine.PeripheralsChanged += (m, e) =>
                {
                    if (e.Operation != PeripheralsChangedEventArgs.PeripheralChangeType.Addition)
                    {
                        nameCache.Invalidate();
                        peripheralToMachineCache.Invalidate();
                    }
                };

                OnMachineAdded(machine);
            };

            machs.ItemRemoved += (name, machine) =>
            {
                machine.StateChanged -= OnMachineStateChanged;
                nameCache.Invalidate();
                peripheralToMachineCache.Invalidate();

                OnMachineRemoved(machine);
            };
            BackendManager = new BackendManager();
            BlobManager = new BlobManager();
            theBag = new Dictionary<string, object>();
            SnapshotTracker = new SnapshotTracker();
        }

        public MasterTimeSource MasterTimeSource { get; private set; }

        public BackendManager BackendManager { get; private set; }

        public SnapshotTracker SnapshotTracker { get; }

        public BlobManager BlobManager { get; set; }

        private EmulationMode mode;
        public EmulationMode Mode 
        { 
            get => mode;
            
            set
            {
                lock(machLock)
                {
                    if(mode != EmulationMode.SynchronizedIO)
                    {
                        var machine = machs.Rights.FirstOrDefault(m => m.HasPlayer || m.HasRecorder);
                        if(machine != null)
                        {
                            throw new RecoverableException($"Could not set the new emulation mode because an event player/recorder is active for machine {machs[machine]}");
                        }
                    }
                    mode = value;
                }
            }
        }

        private readonly object machLock = new object();

        public bool AllMachinesStarted
        {
            get { lock (machLock) { return machs.Rights.All(x => !x.IsPaused); } }
        }

        public bool AnyMachineStarted
        {
            get { lock (machLock) { return machs.Rights.Any(x => !x.IsPaused); } }
        }

        // This property should only be set while holding `machLock`
        public bool IsStarted
        {
            get { lock (machLock) { return isStarted; } }
            private set
            {
                if(isStarted == value)
                {
                    return;
                }

                isStarted = value;
                IsStartedChanged?.Invoke(this, value);
            }
        }

        // Do not access this field directly, use the IsStarted property setter
        [Transient]
        private bool isStarted;

        public IMachine this[String key]
        {
            get { return machs[key]; }
        }

        public String this[IMachine machine]
        {
            get { return machs[machine]; }
        }

        public CachingFileFetcher FileFetcher
        {
            get { return fileFetcher; }
            set { fileFetcher = value; }
        }

        public ExternalsManager ExternalsManager { get; private set; }

        public Connector Connector { get; private set; }

        public MACRepository MACRepository { get; private set; }

        public HostMachine HostMachine { get; private set; }

        public bool TryGetMachine(string key, out IMachine machine)
        {
            return machs.TryGetValue(key, out machine);
        }

        public bool TryGetMachineName(IMachine machine, out string name)
        {
            return machs.TryGetValue(machine, out name);
        }

        public int MachinesCount
        {
            get { return machs.Count; }
        }

        public IEnumerable<IMachine> Machines
        {
            get { return machs.Rights; }
        }

        public IEnumerable<string> Names
        {
            get { return machs.Lefts; }
        }

        public bool TryGetExecutionContext(out IMachine machine, out ICPU cpu)
        {
            foreach(var m in Machines)
            {
                if(m.SystemBus.TryGetCurrentCPU(out cpu))
                {
                    machine = m;
                    return true;
                }
            }

            machine = null;
            cpu = null;
            return false;
        }

        /// <summary>
        /// Adds the machine to emulation.
        /// </summary>
        /// <param name='machine'>
        /// Machine to add.
        /// </param>
        /// <param name='name'>
        /// Name of the machine. If null or empty (as default), the name is automatically given.
        /// </param>
        public void AddMachine(IMachine machine, string name = "")
        {
            if(!TryAddMachine(machine, name))
            {
                throw new RecoverableException("Given machine is already added or name is already taken.");
            }
        }

        public bool TryGetMachineByName(string name, out IMachine machine)
        {
            return machs.TryGetValue(name, out machine);
        }

        public string GetNextMachineName(Platform platform, HashSet<string> reserved = null)
        {
            lock(machLock)
            {
                string name;
                var counter = 0;
                do
                {
                    name = string.Format("{0}-{1}", platform != null ? platform.Name : Machine.MachineKeyword, counter);
                    counter++;
                }
                while(machs.Exists(name) || (reserved != null && reserved.Contains(name)));

                return name;
            }
        }

        public bool TryAddMachine(IMachine machine, string name)
        {
            lock(machLock)
            {
                if(string.IsNullOrEmpty(name))
                {
                    name = GetNextMachineName(machine.Platform);
                }
                else if (machs.ExistsEither(name, machine))
                {
                    return false;
                }

                machs.Add(name, machine);

                if(machine.LocalTimeSource is ITimeSink machineTimeSink)
                {
                    MasterTimeSource.RegisterSink(machineTimeSink);
                }
                else
                {
                    machine.LocalTimeSource = MasterTimeSource;
                }

                return true;
            }
        }

        public void SetSeed(int seed)
        {
            RandomGenerator.ResetSeed(seed);
        }

        public int GetSeed()
        {
            return RandomGenerator.GetCurrentSeed();
        }

        public void RunFor(TimeInterval period)
        {
            if(IsStarted)
            {
                throw new RecoverableException("This action is not available when emulation is already started");
            }
            InnerStartAll();
            MasterTimeSource.RunFor(period);
            PauseAll();
        }

        public void RunToNearestSyncPoint()
        {
            if(IsStarted)
            {
                throw new RecoverableException("This action is not available when emulation is already started");
            }

            InnerStartAll();
            MasterTimeSource.Run();
            PauseAll();
        }

        public void StartAll()
        {
            lock(machLock)
            {
                InnerStartAll();
                MasterTimeSource.Start();
                IsStarted = true;
            }

            System.Threading.Thread.Sleep(100);
        }

        private void InnerStartAll()
        {
            //ToList cast is a precaution for a situation where the list of machines changes
            //during start up procedure. It might happen on rare occasions. E.g. when a script loads them, and user
            //hits the pause button.
            //Otherwise it would crash.
            ExternalsManager.Start();
            foreach(var machine in Machines.ToList())
            {
                machine.Start();
            }
        }

        public void PauseAll()
        {
            lock(machLock)
            {
                MasterTimeSource.Stop();
                Array.ForEach(machs.Rights, x => x.Pause());
                ExternalsManager.Pause();
                IsStarted = false;
            }
        }

        public IDisposable ObtainPausedState()
        {
            return new PausedState(this);
        }

        public IDisposable ObtainSafeState()
        {
            // check if we are on a safe thread that executes sync phase
            if(MasterTimeSource.IsOnSyncPhaseThread)
            {
                return null;
            }

            return ObtainPausedState();
        }

        public AutoResetEvent GetStartedStateChangedEvent(bool requiredStartedState, bool waitForTransition = true)
        {
            var evt = new AutoResetEvent(false);
            lock(machLock)
            {
                if(IsStarted == requiredStartedState && !waitForTransition)
                {
                    evt.Set();
                    return evt;
                }

                Action<Emulation, bool> startedChanged = null;
                startedChanged = (e, started) =>
                {
                    if(started == requiredStartedState)
                    {
                        e.IsStartedChanged -= startedChanged;
                        evt.Set();
                    }
                };

                IsStartedChanged += startedChanged;
                return evt;
            }
        }

        public ILogger CurrentLogger { get; private set; }

        public PseudorandomNumberGenerator RandomGenerator
        {
            get
            {
                return randomGenerator.Value;
            }
        }

        public bool SingleStepBlocking
        {
            get => singleStepBlocking;
            set
            {
                if(singleStepBlocking == value)
                {
                    return;
                }
                singleStepBlocking = value;
                SingleStepBlockingChanged?.Invoke();
            }
        }

        public event Action SingleStepBlockingChanged;

        public void SetNameForMachine(string name, IMachine machine)
        {
            // TODO: locking issues
            IMachine oldMachine;
            machs.TryRemove(name, out oldMachine);

            AddMachine(machine, name);

            var machineExchanged = MachineExchanged;
            if(machineExchanged != null)
            {
                machineExchanged(oldMachine, machine);
            }

            (oldMachine as IDisposable)?.Dispose();
        }

        public void RemoveMachine(string name)
        {
            if(!TryRemoveMachine(name))
            {
                throw new ArgumentException(string.Format("Given machine '{0}' does not exists.", name));
            }
        }

        public void RemoveMachine(IMachine machine)
        {
            machs.Remove(machine);
            (machine as IDisposable)?.Dispose();
        }

        public bool TryRemoveMachine(string name)
        {
            IMachine machine;
            var result = machs.TryRemove(name, out machine);
            if(result)
            {
                (machine as IDisposable)?.Dispose();
            }
            return result;
        }

        public bool TryGetMachineForPeripheral(IPeripheral p, out IMachine machine)
        {
            if(peripheralToMachineCache.TryGetValue(p, out machine))
            {
                return true;
            }

            foreach(var candidate in Machines)
            {
                var candidateAsMachine = candidate;
                if(candidateAsMachine != null && candidateAsMachine.IsRegistered(p))
                {
                    machine = candidateAsMachine;
                    peripheralToMachineCache.Add(p, machine);
                    return true;
                }
            }

            machine = null;
            return false;
        }

        public bool TryGetEmulationElementName(object obj, out string name)
        {
            string localName, localContainerName;
            var result = TryGetEmulationElementName(obj, out localName, out localContainerName);
            name = (localContainerName != null) ? string.Format("{0}:{1}", localContainerName, localName) : localName;
            return result;
        }

        public bool TryGetEmulationElementName(object obj, out string name, out string containerName)
        {
            if(obj == null)
            {
                name = null;
                containerName = null;
                return false;
            }

            Tuple<string, string> result;
            if(nameCache.TryGetValue(obj, out result))
            {
                name = result.Item1;
                containerName = result.Item2;
                return true;
            }

            containerName = null;
            var objAsIPeripheral = obj as IPeripheral;
            if(objAsIPeripheral != null)
            {
                IMachine machine;
                string machName;

                if(TryGetMachineForPeripheral(objAsIPeripheral, out machine) && TryGetMachineName(machine, out machName))
                {
                    containerName = machName;
                    if(Misc.IsPythonObject(obj))
                    {
                        name = Misc.GetPythonName(obj);
                    }
                    else
                    {
                        if(!machine.TryGetAnyName(objAsIPeripheral, out name))
                        {
                            name = Machine.UnnamedPeripheral;
                        }
                    }
                    nameCache.Add(obj, Tuple.Create(name, containerName));
                    return true;
                }
            }
            var objAsMachine = obj as Machine;
            if(objAsMachine != null)
            {
                if(EmulationManager.Instance.CurrentEmulation.TryGetMachineName(objAsMachine, out name))
                {
                    nameCache.Add(obj, Tuple.Create(name, containerName));
                    return true;
                }
            }
            var objAsIExternal = obj as IExternal;
            if(objAsIExternal != null)
            {
                if(ExternalsManager.TryGetName(objAsIExternal, out name))
                {
                    nameCache.Add(obj, Tuple.Create(name, containerName));
                    return true;
                }
            }

            var objAsIHostMachineElement = obj as IHostMachineElement;
            if(objAsIHostMachineElement != null)
            {
                if(HostMachine.TryGetName(objAsIHostMachineElement, out name))
                {
                    containerName = HostMachine.HostMachineName;
                    nameCache.Add(obj, Tuple.Create(name, containerName));
                    return true;
                }
            }

            name = null;
            return false;
        }

        public bool TryGetEmulationElementByName(string name, object context, out IEmulationElement element)
        {
            if(name == null)
            {
                element = null;
                return false;
            }
            var machineContext = context as Machine;
            if(machineContext != null)
            {
                IPeripheral outputPeripheral;
                if((machineContext.TryGetByName(name, out outputPeripheral) || machineContext.TryGetByName(string.Format("sysbus.{0}", name), out outputPeripheral)))
                {
                    element = outputPeripheral;
                    return true;
                }
            }

            IMachine machine;
            if(TryGetMachineByName(name, out machine))
            {
                element = machine;
                return true;
            }

            IExternal external;
            if(ExternalsManager.TryGetByName(name, out external))
            {
                element = external;
                return true;
            }

            IHostMachineElement hostMachineElement;
            if(name.StartsWith(string.Format("{0}.", HostMachine.HostMachineName))
                && HostMachine.TryGetByName(name.Substring(HostMachine.HostMachineName.Length + 1), out hostMachineElement))
            {
                element = hostMachineElement;
                return true;
            }

            element = null;
            return false;
        }

        public void Dispose()
        {
            FileFetcher.CancelDownload();
            lock(machLock)
            {
                PauseAll();
                // dispose externals before machines;
                // some externals, e.g. execution tracer,
                // require access to peripherals when operating
                ExternalsManager.Clear();
                BackendManager.Dispose();
                Array.ForEach(machs.Rights, x => (x as IDisposable)?.Dispose());
                MasterTimeSource.Dispose();
                machs.Dispose();
                HostMachine.Dispose();
                CurrentLogger.Dispose();
                FileFetcher.Dispose();
            }
        }

        public void AddOrUpdateInBag<T>(string name, T value) where T : class
        {
            lock(theBag)
            {
                theBag[name] = value;
            }
        }

        public void TryRemoveFromBag(string name)
        {
            lock(theBag)
            {
                if(theBag.ContainsKey(name))
                {
                    theBag.Remove(name);
                }
            }
        }

        public bool TryGetFromBag<T>(string name, out T value) where T : class
        {
            lock(theBag)
            {
                if(theBag.ContainsKey(name))
                {
                    value = theBag[name] as T;
                    if(value != null)
                    {
                        return true;
                    }
                }
                value = null;
                return false;
            }
        }


        [field: Transient]
        public event Action<IMachine, IMachine> MachineExchanged;

        [PostDeserialization]
        private void AfterDeserialization()
        {
            // recreate events
            foreach(var mach in machs.Rights)
            {
                mach.StateChanged += OnMachineStateChanged;
            }
            singleStepBlocking = true;
        }

        #region Event processors

        private void OnMachineStateChanged(IMachine machine, MachineStateChangedEventArgs ea)
        {
            var msc = MachineStateChanged;
            if(msc != null)
            {
                msc(machine, ea);
            }
        }

        private void OnMachineAdded(IMachine machine)
        {
            var ma = MachineAdded;
            if(ma != null)
            {
                ma(machine);
            }
        }

        private void OnMachineRemoved(IMachine machine)
        {
            var mr = MachineRemoved;
            if(mr != null)
            {
                mr(machine);
            }
        }

        #endregion

        [field: Transient]
        public event Action<IMachine, MachineStateChangedEventArgs> MachineStateChanged;

        [field: Transient]
        public event Action<IMachine> MachineAdded;
        [field: Transient]
        public event Action<IMachine> MachineRemoved;

        [field: Transient]
        public event Action<Emulation, bool> IsStartedChanged;

        [Constructor]
        private CachingFileFetcher fileFetcher;

        [field: Transient]
        private bool singleStepBlocking = true;

        [Constructor(NameCacheSize)]
        private readonly LRUCache<object, Tuple<string, string>> nameCache;

        [Constructor(PeripheralToMachineCacheSize)]
        private readonly LRUCache<IPeripheral, IMachine> peripheralToMachineCache;

        private readonly Lazy<PseudorandomNumberGenerator> randomGenerator;
        private readonly Dictionary<string, object> theBag;
        private readonly FastReadConcurrentTwoWayDictionary<string, IMachine> machs;

        private const int NameCacheSize = 100;
        private const int PeripheralToMachineCacheSize = 100;
        
        public enum EmulationMode
        {
            SynchronizedIO,
            SynchronizedTimers
        }

        private class PausedState : IDisposable
        {
            public PausedState(Emulation emulation)
            {
                wasStarted = emulation.IsStarted;
                this.emulation = emulation;

                if(wasStarted)
                {
                    emulation.MasterTimeSource.Stop();
                    machineStates = emulation.Machines.Select(x => x.ObtainPausedState()).ToArray();
                    emulation.ExternalsManager.Pause();
                }
            }

            public void Dispose()
            {
                if(!wasStarted)
                {
                    return;
                }

                emulation.MasterTimeSource.Start();
                foreach(var state in machineStates)
                {
                    state.Dispose();
                }
                emulation.ExternalsManager.Start();
            }

            private readonly IDisposable[] machineStates;
            private readonly Emulation emulation;
            private readonly bool wasStarted;
        }
    }
}

