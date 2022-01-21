//
// Copyright (c) 2010-2022 Antmicro
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
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities.Collections;

namespace Antmicro.Renode.Core
{
    public class Emulation : IInterestingType, IDisposable
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
            peripheralToMachineCache = new LRUCache<IPeripheral, Machine>(PeripheralToMachineCacheSize);

            machs = new FastReadConcurrentTwoWayDictionary<string, Machine>();
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
        }

        public MasterTimeSource MasterTimeSource { get; private set; }

        public BackendManager BackendManager { get; private set; }

        public BlobManager BlobManager { get; set; }

        private readonly object machLock = new object();

        public bool AllMachinesStarted
        {
            get { lock (machLock) { return machs.Rights.All(x => !x.IsPaused); } }
        }

        public bool AnyMachineStarted
        {
            get { lock (machLock) { return machs.Rights.Any(x => !x.IsPaused); } }
        }

        public bool IsStarted
        {
            get { return isStarted; }
            private set { isStarted = value; }
        }

        [Transient]
        private bool isStarted;

        public Machine this[String key]
        {
            get { return machs[key]; }
        }

        public String this[Machine machine]
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

        public bool TryGetMachineName(Machine machine, out string name)
        {
            return machs.TryGetValue(machine, out name);
        }

        public int MachinesCount
        {
            get { return machs.Count; }
        }

        public IEnumerable<Machine> Machines
        {
            get { return machs.Rights; }
        }

        public IEnumerable<string> Names
        {
            get { return machs.Lefts; }
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
        public void AddMachine(Machine machine, string name = "")
        {
            if(!TryAddMachine(machine, name))
            {
                throw new RecoverableException("Given machine is already added or name is already taken.");
            }
        }

        public bool TryGetMachineByName(string name, out Machine machine)
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

        public bool TryAddMachine(Machine machine, string name)
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
                MasterTimeSource.RegisterSink(machine.LocalTimeSource);
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
            IsStarted = true;
            InnerStartAll();
            MasterTimeSource.Start();

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
                Array.ForEach(machs.Rights, x => x.Pause());
                ExternalsManager.Pause();
                MasterTimeSource.Stop();
                IsStarted = false;
            }
        }

        public IDisposable ObtainPausedState()
        {
            return new PausedState(this);
        }

        public ILogger CurrentLogger { get; private set; }

        public PseudorandomNumberGenerator RandomGenerator
        {
            get
            {
                return randomGenerator.Value;
            }
        }

        public void SetNameForMachine(string name, Machine machine)
        {
            // TODO: locking issues
            Machine oldMachine;
            machs.TryRemove(name, out oldMachine);

            AddMachine(machine, name);

            var machineExchanged = MachineExchanged;
            if(machineExchanged != null)
            {
                machineExchanged(oldMachine, machine);
            }

            oldMachine.Dispose();
        }

        public void RemoveMachine(string name)
        {
            if(!TryRemoveMachine(name))
            {
                throw new ArgumentException(string.Format("Given machine '{0}' does not exists.", name));
            }
        }

        public void RemoveMachine(Machine machine)
        {
            machs.Remove(machine);
            machine.Dispose();
        }

        public bool TryRemoveMachine(string name)
        {
            Machine machine;
            var result = machs.TryRemove(name, out machine);
            if(result)
            {
                machine.Dispose();
            }
            return result;
        }

        public bool TryGetMachineForPeripheral(IPeripheral p, out Machine machine)
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
                Machine machine;
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

            Machine machine;
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
                Array.ForEach(machs.Rights, x => x.Dispose());
                MasterTimeSource.Dispose();
                machs.Dispose();
                HostMachine.Dispose();
                CurrentLogger.Dispose();
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
        public event Action<Machine, Machine> MachineExchanged;

        [PostDeserialization]
        private void AfterDeserialization()
        {
            // recreate events
            foreach(var mach in machs.Rights)
            {
                mach.StateChanged += OnMachineStateChanged;
            }
        }

        #region Event processors

        private void OnMachineStateChanged(Machine machine, MachineStateChangedEventArgs ea)
        {
            var msc = MachineStateChanged;
            if(msc != null)
            {
                msc(machine, ea);
            }
        }

        private void OnMachineAdded(Machine machine)
        {
            var ma = MachineAdded;
            if(ma != null)
            {
                ma(machine);
            }
        }

        private void OnMachineRemoved(Machine machine)
        {
            var mr = MachineRemoved;
            if(mr != null)
            {
                mr(machine);
            }
        }

        #endregion

        [field: Transient]
        public event Action<Machine, MachineStateChangedEventArgs> MachineStateChanged;

        [field: Transient]
        public event Action<Machine> MachineAdded;
        [field: Transient]
        public event Action<Machine> MachineRemoved;

        [Constructor]
        private CachingFileFetcher fileFetcher;

        [Constructor(NameCacheSize)]
        private readonly LRUCache<object, Tuple<string, string>> nameCache;

        [Constructor(PeripheralToMachineCacheSize)]
        private readonly LRUCache<IPeripheral, Machine> peripheralToMachineCache;

        private readonly Lazy<PseudorandomNumberGenerator> randomGenerator;
        private readonly Dictionary<string, object> theBag;
        private readonly FastReadConcurrentTwoWayDictionary<string, Machine> machs;

        private const int NameCacheSize = 100;
        private const int PeripheralToMachineCacheSize = 100;

        private class PausedState : IDisposable
        {
            public PausedState(Emulation emulation)
            {
                wasStarted = emulation.IsStarted;
                this.emulation = emulation;

                if(wasStarted)
                {
                    machineStates = emulation.Machines.Select(x => x.ObtainPausedState()).ToArray();
                    emulation.ExternalsManager.Pause();
                    emulation.MasterTimeSource.Stop();
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

