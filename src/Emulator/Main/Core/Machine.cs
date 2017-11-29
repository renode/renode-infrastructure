//
// Copyright (c) 2010-2017 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Antmicro.Migrant;
using System.Threading;
using Antmicro.Renode.Time;
using System.Text;
using Antmicro.Renode.Peripherals.CPU;
using System.Reflection;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.UserInterface;
using Antmicro.Renode.EventRecording;
using System.IO;
using System.Diagnostics;

namespace Antmicro.Renode.Core
{
    public class Machine : IEmulationElement, IDisposable, ISynchronized
    {
        public Machine()
        {
            collectionSync = new object();
            pausingSync = new object();
            disposedSync = new object();
            hostTimeClockSource = new HostTimeClockSource();
            clockSourceWrapper = new ClockSourceWrapper(hostTimeClockSource, this);
            stopwatch = new Stopwatch();
            localNames = new Dictionary<IPeripheral, string>();
            PeripheralsGroups = new PeripheralsGroupsManager(this);
            ownLifes = new HashSet<IHasOwnLife>();
            syncedManagedThreads = new List<SynchronizedManagedThread>();
            delayedTasks = new SortedSet<DelayedTask>();
            pausedState = new PausedState(this);
            SystemBus = new SystemBus(this);
            registeredPeripherals = new MultiTree<IPeripheral, IRegistrationPoint>(SystemBus);
            userStateHook = delegate
            {
            };
            userState = string.Empty;
            SetLocalName(SystemBus, SystemBusName);
            clockSourceWrapper.AddClockEntry(new ClockEntry(1, -DefaultSyncUnit, SynchronizeInDomain).With(value: 0));
            clockSourceWrapper.AddClockEntry(new ClockEntry(uint.MaxValue, ClockEntry.FrequencyToRatio(this, 1000), IndicatorForClockSource));
            syncDomain = new DummySynchronizationDomain();
            currentSynchronizer = syncDomain.ProvideSynchronizer();
        }

        public IEnumerable<IPeripheral> GetParentPeripherals(IPeripheral peripheral)
        {
            var node = registeredPeripherals.TryGetNode(peripheral);
            return node == null ? new IPeripheral[0] : node.Parents.Select(x => x.Value).Distinct();
        }

        public IEnumerable<IPeripheral> GetChildrenPeripherals(IPeripheral peripheral)
        {
            var node = registeredPeripherals.TryGetNode(peripheral);
            return node == null ? new IPeripheral[0] : node.Children.Select(x => x.Value).Distinct();
        }

        public IEnumerable<IRegistrationPoint> GetPeripheralRegistrationPoints(IPeripheral parentPeripheral, IPeripheral childPeripheral)
        {
            var parentNode = registeredPeripherals.TryGetNode(parentPeripheral);
            return parentNode == null ? new IRegistrationPoint[0] : parentNode.GetConnectionWays(childPeripheral);
        }

        public void RegisterAsAChildOf(IPeripheral peripheralParent, IPeripheral peripheralChild, IRegistrationPoint registrationPoint)
        {
            Register(peripheralChild, registrationPoint, peripheralParent);
        }

        public void UnregisterAsAChildOf(IPeripheral peripheralParent, IPeripheral peripheralChild)
        {
            lock(collectionSync)
            {
                CollectGarbageStamp();
                IPeripheralsGroup group;
                if(PeripheralsGroups.TryGetActiveGroupContaining(peripheralChild, out group))
                {
                    throw new RegistrationException(string.Format("Given peripheral is a member of '{0}' peripherals group and cannot be directly removed.", group.Name));
                }

                var parentNode = registeredPeripherals.GetNode(peripheralParent);
                parentNode.RemoveChild(peripheralChild);
                EmulationManager.Instance.CurrentEmulation.BackendManager.HideAnalyzersFor(peripheralChild);
                CollectGarbage();
            }
        }

        public void UnregisterAsAChildOf(IPeripheral peripheralParent, IRegistrationPoint registrationPoint)
        {
            lock(collectionSync)
            {
                CollectGarbageStamp();
                try
                {
                    var parentNode = registeredPeripherals.GetNode(peripheralParent);
                    IPeripheral removedPeripheral = null;
                    parentNode.RemoveChild(registrationPoint, p =>
                    {
                        IPeripheralsGroup group;
                        if(PeripheralsGroups.TryGetActiveGroupContaining(p, out group))
                        {
                            throw new RegistrationException(string.Format("Given peripheral is a member of '{0}' peripherals group and cannot be directly removed.", group.Name));
                        }
                        removedPeripheral = p;
                        return true;
                    });
                    CollectGarbage();
                    if(removedPeripheral != null && registeredPeripherals.TryGetNode(removedPeripheral) == null)
                    {
                        EmulationManager.Instance.CurrentEmulation.BackendManager.HideAnalyzersFor(removedPeripheral);
                    }
                }
                catch(RegistrationException)
                {
                    CollectGarbage();
                    throw;
                }
            }
        }

        public void UnregisterFromParent(IPeripheral peripheral)
        {
            InnerUnregisterFromParent(peripheral);
            OnMachinePeripheralsChanged(peripheral, PeripheralsChangedEventArgs.PeripheralChangeType.CompleteRemoval);
        }

        public IEnumerable<T> GetPeripheralsOfType<T>()
        {
            return GetPeripheralsOfType(typeof(T)).Cast<T>();
        }

        public IEnumerable<IPeripheral> GetPeripheralsOfType(Type t)
        {
            lock(collectionSync)
            {
                return registeredPeripherals.Values.Where(t.IsInstanceOfType).ToList();
            }
        }

        public IEnumerable<PeripheralTreeEntry> GetRegisteredPeripherals()
        {
            var result = new List<PeripheralTreeEntry>();
            lock(collectionSync)
            {
                registeredPeripherals.TraverseWithConnectionWaysParentFirst((currentNode, regPoint, parent, level) =>
                {
                    string localName;
                    TryGetLocalName(currentNode.Value, out localName);
                    result.Add(new PeripheralTreeEntry(currentNode.Value, parent, currentNode.Value.GetType(), regPoint, localName, level));
                }, 0);
            }
            return result;
        }

        public bool TryGetByName<T>(string name, out T peripheral, out string longestMatch) where T : class, IPeripheral
        {
            if(name == null)
            {
                longestMatch = string.Empty;
                peripheral = null;
                return false;
            }
            var splitPath = name.Split(new [] { '.' }, 2);
            if(splitPath.Length == 1 && name == SystemBusName)
            {
                longestMatch = name;
                peripheral = (T)(IPeripheral)SystemBus;
                return true;
            }

            if(splitPath[0] != SystemBusName)
            {
                longestMatch = string.Empty;
                peripheral = null;
                return false;
            }

            MultiTreeNode<IPeripheral, IRegistrationPoint> result;
            if(TryFindSubnodeByName(registeredPeripherals.GetNode(SystemBus), splitPath[1], out result, SystemBusName, out longestMatch))
            {
                peripheral = (T)result.Value;
                return true;
            }
            peripheral = null;
            return false;
        }

        public bool TryGetByName<T>(string name, out T peripheral) where T : class, IPeripheral
        {
            string fake;
            return TryGetByName(name, out peripheral, out fake);
        }

        public string GetLocalName(IPeripheral peripheral)
        {
            string result;
            lock(collectionSync)
            {
                if(!TryGetLocalName(peripheral, out result))
                {
                    throw new KeyNotFoundException();
                }
                return result;
            }
        }

        public bool TryGetLocalName(IPeripheral peripheral, out string name)
        {
            lock(collectionSync)
            {
                return localNames.TryGetValue(peripheral, out name);
            }
        }

        public void SetLocalName(IPeripheral peripheral, string name)
        {
            if(string.IsNullOrEmpty(name))
            {
                throw new RecoverableException("The name of the peripheral cannot be null nor empty.");
            }
            lock(collectionSync)
            {
                if(!registeredPeripherals.ContainsValue(peripheral))
                {
                    throw new RecoverableException("Cannot name peripheral which is not registered.");
                }
                if(localNames.ContainsValue(name))
                {
                    throw new RecoverableException(string.Format("Given name '{0}' is already used.", name));
                }
                localNames[peripheral] = name;
            }

            var pc = PeripheralsChanged;
            if(pc != null)
            {
                pc(this, new PeripheralsChangedEventArgs(peripheral, PeripheralsChangedEventArgs.PeripheralChangeType.NameChanged));
            }
        }

        public IEnumerable<string> GetAllNames()
        {
            var nameSegments = new AutoResizingList<string>();
            var names = new List<string>();
            lock(collectionSync)
            {
                registeredPeripherals.TraverseParentFirst((x, y) =>
                {
                    if(!localNames.ContainsKey(x))
                    {
                        // unnamed node
                        return;
                    }
                    var localName = localNames[x];
                    nameSegments[y] = localName;
                    var globalName = new StringBuilder();
                    for(var i = 0; i < y; i++)
                    {
                        globalName.Append(nameSegments[i]);
                        globalName.Append(PathSeparator);
                    }
                    globalName.Append(localName);
                    names.Add(globalName.ToString());
                }, 0);
            }
            return new ReadOnlyCollection<string>(names);
        }

        public bool TryGetAnyName(IPeripheral peripheral, out string name)
        {
            var names = GetNames(peripheral);
            if(names.Count > 0)
            {
                name = names[0];
                return true;
            }
            name = null;
            return false;
        }

        public string GetAnyNameOrTypeName(IPeripheral peripheral)
        {
            string name;
            if(!TryGetAnyName(peripheral, out name))
            {
                var managedThread = peripheral as IManagedThread;
                return managedThread != null ? managedThread.ToString() : peripheral.GetType().Name;
            }
            return name;
        }

        public bool IsRegistered(IPeripheral peripheral)
        {
            lock(collectionSync)
            {
                return registeredPeripherals.ContainsValue(peripheral);
            }
        }

        public IDisposable ObtainPausedState()
        {
            return pausedState.Enter();
        }

        public void Start()
        {
            lock(pausingSync)
            {
                switch(state)
                {
                case State.Started:
                    return;
                case State.Paused:
                    Resume();
                    return;
                }
                stopwatch.Start();
                machineStartedAt = CustomDateTime.Now;
                foreach(var ownLife in ownLifes.OrderBy(x => x is ICPU ? 1 : 0))
                {
                    this.NoisyLog("Starting {0}.", GetNameForOwnLife(ownLife));
                    ownLife.Start();
                }
                hostTimeClockSource.Start();
                this.Log(LogLevel.Info, "Machine started.");
                state = State.Started;
                var machineStarted = StateChanged;
                if(machineStarted != null)
                {
                    machineStarted(this, new MachineStateChangedEventArgs(MachineStateChangedEventArgs.State.Started));
                }
            }
        }

        public void Pause()
        {
            stopwatch.Stop();
            lock(pausingSync)
            {
                switch(state)
                {
                case State.Paused:
                    return;
                case State.NotStarted:
                    goto case State.Paused;
                }
                currentSynchronizer.CancelSync();
                hostTimeClockSource.Pause();
                foreach(var ownLife in ownLifes.OrderBy(x => x is ICPU ? 0 : 1))
                {
                    var ownLifeName = GetNameForOwnLife(ownLife);
                    this.NoisyLog("Pausing {0}.", ownLifeName);
                    ownLife.Pause();
                    this.NoisyLog("{0} paused.", ownLifeName);
                }
                state = State.Paused;
                var machinePaused = StateChanged;
                if(machinePaused != null)
                {
                    machinePaused(this, new MachineStateChangedEventArgs(MachineStateChangedEventArgs.State.Paused));
                }
                this.Log(LogLevel.Info, "Machine paused.");
            }
        }

        public void Reset()
        {
            lock(pausingSync)
            {
                if(state == State.NotStarted)
                {
                    this.DebugLog("Reset request: doing nothing, because system is not started.");
                    return;
                }
                using(ObtainPausedState())
                {
                    foreach(var resetable in registeredPeripherals.Distinct())
                    {
                        if(resetable == this)
                        {
                            continue;
                        }
                        resetable.Reset();
                    }
                    var machineReset = MachineReset;
                    if(machineReset != null)
                    {
                        machineReset(this);
                    }
                }
            }
        }

        public void Dispose()
        {
            lock(disposedSync)
            {
                if(alreadyDisposed)
                {
                    return;
                }
                alreadyDisposed = true;
            }
            Pause();
            currentSynchronizer.Exit();
            if(recorder != null)
            {
                recorder.Dispose();
            }
            if(player != null)
            {
                player.Dispose();
                var currentSyncDomain = syncDomain as SynchronizationDomain;
                if(currentSyncDomain != null)
                {
                    currentSyncDomain.SyncPointReached -= player.Play;
                }
            }

            // ordering below is due to the fact that the CPU can use other peripherals, e.g. Memory so it should be disposed last
            foreach(var peripheral in GetPeripheralsOfType<IDisposable>().OrderBy(x => x is ICPU ? 0 : 1))
            {
                this.DebugLog("Disposing {0}.", GetAnyNameOrTypeName((IPeripheral)peripheral));
                peripheral.Dispose();
            }
            this.Log(LogLevel.Info, "Disposed.");
            var disposed = StateChanged;
            if(disposed != null)
            {
                disposed(this, new MachineStateChangedEventArgs(MachineStateChangedEventArgs.State.Disposed));
            }

            EmulationManager.Instance.CurrentEmulation.BackendManager.HideAnalyzersFor(this);
        }

        public IManagedThread ObtainManagedThread(Action action, object owner, uint frequency, string name = null, bool synchronized = true)
        {
            var ownerName = owner.GetType().Name;
            if(!synchronized)
            {
                var managedThread = new ManagedThread(action, owner, this, name, frequency);
                RegisterManagedThread(managedThread);
                return managedThread;
            }
            var maximalFrequencyWithCurrentSyncUnit = Antmicro.Renode.Time.Consts.TicksPerSecond / SyncUnit;
            if(maximalFrequencyWithCurrentSyncUnit < frequency)
            {
                switch(ConfigurationManager.Instance.Get<SyncUnitPolicy>("time", "sync-unit-policy", SyncUnitPolicy.ShowWarning))
                {
                case SyncUnitPolicy.ShowWarning:
                    this.Log(LogLevel.Warning, "Desired frequency of managed thread '{0}:{1}' is {2}Hz while maximal allowed by current sync unit is {3}Hz",
                        ownerName, name, Misc.NormalizeDecimal(frequency), Misc.NormalizeDecimal(maximalFrequencyWithCurrentSyncUnit));
                    break;
                case SyncUnitPolicy.Adjust:
                    var desiredSyncUnit = Antmicro.Renode.Time.Consts.TicksPerSecond / frequency;
                    if(desiredSyncUnit == 0)
                    {
                        desiredSyncUnit = 1;
                        this.Log(LogLevel.Warning, "Desired frequency of managed thread '{0}:{1}' is {2}Hz which is unattainable even with sync unit = 1",
                            ownerName, name, Misc.NormalizeDecimal(frequency));
                    }
                    else
                    {
                        this.Log(LogLevel.Info, "Setting sync unit to value {0} due to frequency {1}Hz of managed thread '{2}:{3}'",
                            desiredSyncUnit, Misc.NormalizeDecimal(frequency), ownerName, name);
                    }
                    SyncUnit = desiredSyncUnit;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
                }
            }
            var syncedThread = new SynchronizedManagedThread(action, owner, this, name, frequency);
            syncedManagedThreads.Add(syncedThread);
            return syncedThread;
        }

        public IClockSource ObtainClockSource()
        {
            return clockSourceWrapper;
        }

        public void SetClockSource(IClockSource clockSource)
        {
            if(!(syncDomain is DummySynchronizationDomain) && clockSource is HostTimeClockSource)
            {
                throw new RecoverableException("You cannot set the host time clock source when synchronization domain is used.");
            }
            clockSourceWrapper.CurrentClockSource = clockSource;
        }

        [UiAccessible]
        public string[,] GetClockSourceInfo()
        {
            var entries = clockSourceWrapper.GetAllClockEntries();

            var table = new Table().AddRow("Owner", "Enabled", "Frequency", "Limit", "Event frequency", "Event period");
            table.AddRows(entries, x =>
            {
                var owner = x.Handler.Target;
                var ownerAsPeripheral = owner as IPeripheral;
                return ownerAsPeripheral != null ? GetAnyNameOrTypeName(ownerAsPeripheral) : owner.GetType().Name;
            },
                x => x.Enabled.ToString(),
                x => Misc.NormalizeDecimal(x.Frequency) + "Hz",
                x => x.Period.ToString(),
                x => Misc.NormalizeDecimal(x.Frequency / x.Period) + "Hz",
                x => Misc.NormalizeDecimal(1.0 / (x.Frequency / x.Period)) + "s"
            );
            return table.ToArray();
        }

        public void SetHostTimeClockSource()
        {
            clockSourceWrapper.CurrentClockSource = hostTimeClockSource;
        }

        public DateTime GetRealTimeClockBase()
        {
            switch(RealTimeClockMode)
            {
            case RealTimeClockMode.VirtualTime:
                return new DateTime(1970, 1, 1) + ElapsedVirtualTime;
            case RealTimeClockMode.VirtualTimeWithHostBeginning:
                return machineStartedAt + ElapsedVirtualTime;
            default:
                throw new ArgumentOutOfRangeException();
            }
        }

        public void ExecuteIn(Action what, TimeSpan when = default(TimeSpan))
        {
            if(clockSourceWrapper.CurrentClockSource is HostTimeClockSource)
            {
                throw new InvalidOperationException("This function can only be used with virtual timers.");
            }
            delayedTasks.Add(new DelayedTask(what, timeSpanBySyncSoFar + when));
        }

        public void AttachGPIO(IPeripheral source, int sourceNumber, IGPIOReceiver destination, int destinationNumber, int? localReceiverNumber = null)
        {
            var sourceByNumber = source as INumberedGPIOOutput;
            IGPIO igpio;
            if(sourceByNumber == null)
            {
                throw new RecoverableException("Source peripheral cannot be connected by number.");
            }
            if(!sourceByNumber.Connections.TryGetValue(sourceNumber, out igpio))
            {
                throw new RecoverableException(string.Format("Source peripheral has no GPIO number: {0}", source));
            }
            var actualDestination = GetActualReceiver(destination, localReceiverNumber);
            igpio.Connect(actualDestination, destinationNumber);
        }

        public void AttachGPIO(IPeripheral source, IGPIOReceiver destination, int destinationNumber, int? localReceiverNumber = null)
        {
            var connectors = source.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(x => typeof(GPIO).IsAssignableFrom(x.PropertyType)).ToArray();
            var actualDestination = GetActualReceiver(destination, localReceiverNumber);
            DoAttachGPIO(source, connectors, actualDestination, destinationNumber);
        }

        public void AttachGPIO(IPeripheral source, string connectorName, IGPIOReceiver destination, int destinationNumber, int? localReceiverNumber = null)
        {
            var connectors = source.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(x => x.Name == connectorName && typeof(GPIO).IsAssignableFrom(x.PropertyType)).ToArray();
            var actualDestination = GetActualReceiver(destination, localReceiverNumber);
            DoAttachGPIO(source, connectors, actualDestination, destinationNumber);
        }

        public void ReportForeignEvent<T>(T handlerArgument, Action<T> handler)
        {
            ReportForeignEventInner((syncNumber, eventNotFromDomain) => recorder.Record(handlerArgument, handler, syncNumber, eventNotFromDomain),
                () => handler(handlerArgument));
        }

        public void ReportForeignEvent<T1, T2>(T1 handlerArgument1, T2 handlerArgument2, Action<T1, T2> handler)
        {
            ReportForeignEventInner((syncNumber, 
                eventNotFromDomain) => recorder.Record(handlerArgument1, handlerArgument2, handler, syncNumber, eventNotFromDomain),
                () => handler(handlerArgument1, handlerArgument2));
        }

        public void RecordTo(string fileName, RecordingBehaviour recordingBehaviour)
        {
            var currentSyncDomain = syncDomain as SynchronizationDomain;
            if(currentSyncDomain == null)
            {
                throw new RecoverableException("You can only record events on the fully fledged synchronization domain.");
            }
            recorder = new Recorder(File.Create(fileName), this, recordingBehaviour);
        }

        public void PlayFrom(string fileName)
        {
            var currentSyncDomain = syncDomain as SynchronizationDomain;
            if(currentSyncDomain == null)
            {
                throw new RecoverableException("You can only play recorded events on the fully fledged synchronization domain.");
            }
            player = new Player(File.OpenRead(fileName), this);
            currentSyncDomain.SyncPointReached += player.Play;
        }

        public void AddUserStateHook(Func<string, bool> predicate, Action<string> hook)
        {
            userStateHook += currentState =>
            {
                if(predicate(currentState))
                {
                    hook(currentState);
                }
            };
        }

        public override string ToString()
        {
            return EmulationManager.Instance.CurrentEmulation[this];
        }

        public IPeripheral this[string name]
        {
            get
            {
                return GetByName(name);
            }
        }

        public string UserState
        {
            get
            {
                return userState;
            }
            set
            {
                userState = value;
                userStateHook(userState);
            }
        }

        public SystemBus SystemBus { get; private set; }

        public IPeripheralsGroupsManager PeripheralsGroups { get; private set; }

        public Platform Platform { get; set; }

        public bool IsPaused
        {
            get
            {
                // locking on pausingSync can couse deadlock (when mach.Start() and AllMachineStarted are called together)
                var stateCopy = state;
                return stateCopy == State.Paused || stateCopy == State.NotStarted;
            }
        }

        public TimeSpan ElapsedVirtualTime
        {
            get
            {
                return TimeSpan.FromMilliseconds(clockSourceWrapper.GetClockEntry(IndicatorForClockSource).Value);
            }
        }

        public TimeSpan ElapsedHostTime
        {
            get
            {
                return stopwatch.Elapsed;
            }
        }

        public HostTimeClockSource HostTimeClockSource
        {
            get
            {
                return hostTimeClockSource;
            }
        }

        public ISynchronizationDomain SyncDomain
        {
            get
            {
                return syncDomain;
            }
            set
            {
                if(clockSourceWrapper.CurrentClockSource is HostTimeClockSource)
                {
                    throw new RecoverableException("One cannot change synchronization domain when host time clock source is used.");
                }
                currentSynchronizer.Exit();
                currentSynchronizer = value.ProvideSynchronizer();
                syncDomain = value;
            }
        }

        public ulong SyncUnit
        {
            get
            {
                return (ulong)(-clockSourceWrapper.GetClockEntry(SynchronizeInDomain).Ratio);
            }
            set
            {
                // nope, this is not a bug - since we use Ratio that is long we cannot accept too long values
                // we could make `SyncUnit` a long but we then we should not accept negative values, so it does not matter which one we choose
                if(value > long.MaxValue)
                {
                    throw new RecoverableException("SyncUnit cannot be too long");
                }
                clockSourceWrapper.ExchangeClockEntryWith(SynchronizeInDomain, entry => entry.With(ratio: -1 * (long)value));
            }
        }

        public RealTimeClockMode RealTimeClockMode { get; set; }

        [field: Transient]
        public event Action<Machine, MachineStateChangedEventArgs> StateChanged;
        [field: Transient]
        public event Action<Machine, PeripheralsChangedEventArgs> PeripheralsChanged;
        [field: Transient]
        public event Action<Machine> MachineReset;

        public const char PathSeparator = '.';
        public const string SystemBusName = "sysbus";
        public const string UnnamedPeripheral = "[no-name]";
        public const string MachineKeyword = "machine";

        private void InnerUnregisterFromParent(IPeripheral peripheral)
        {
            using(ObtainPausedState())
            {
                lock(collectionSync)
                {
                    var parents = GetParents(peripheral);
                    if(parents.Count > 1)
                    {
                        throw new RegistrationException(string.Format("Given peripheral is connected to more than one different parent, at least '{0}' and '{1}'.",
                            parents.Select(x => GetAnyNameOrTypeName(x)).Take(2).ToArray()));
                    }

                    IPeripheralsGroup group;
                    if(PeripheralsGroups.TryGetActiveGroupContaining(peripheral, out group))
                    {
                        throw new RegistrationException(string.Format("Given peripheral is a member of '{0}' peripherals group and cannot be directly removed.", group.Name));
                    }

                    var parent = parents.FirstOrDefault();
                    if(parent == null)
                    {
                        throw new RecoverableException(string.Format("Cannot unregister peripheral {0} since it does not have any parent.", peripheral));
                    }
                    ((dynamic)parent).Unregister((dynamic)peripheral);
                    EmulationManager.Instance.CurrentEmulation.BackendManager.HideAnalyzersFor(peripheral);
                }
            }
        }

        private void Register(IPeripheral peripheral, IRegistrationPoint registrationPoint, IPeripheral parent)
        {
            using(ObtainPausedState())
            {
                Action executeAfterLock = null;
                lock(collectionSync)
                {
                    var parentNode = registeredPeripherals.GetNode(parent);
                    parentNode.AddChild(peripheral, registrationPoint);
                    var ownLife = peripheral as IHasOwnLife;
                    if(ownLife != null)
                    {
                        ownLifes.Add(ownLife);
                        if(state == State.Paused)
                        {
                            executeAfterLock = delegate
                            {
                                ownLife.Start();
                                ownLife.Pause();
                            };
                        }
                    }
                }
                if(executeAfterLock != null)
                {
                    executeAfterLock();
                }
            }

            OnMachinePeripheralsChanged(peripheral, PeripheralsChangedEventArgs.PeripheralChangeType.Addition);
            EmulationManager.Instance.CurrentEmulation.BackendManager.TryCreateBackend(peripheral);
        }

        private void OnMachinePeripheralsChanged(IPeripheral peripheral, PeripheralsChangedEventArgs.PeripheralChangeType operation)
        {
            var mpc = PeripheralsChanged;
            if(mpc != null)
            {
                mpc(this, new PeripheralsChangedEventArgs(peripheral, operation));
            }
        }

        private bool TryFindSubnodeByName(MultiTreeNode<IPeripheral, IRegistrationPoint> from, string path, out MultiTreeNode<IPeripheral, IRegistrationPoint> subnode,
            string currentMatching, out string longestMatching)
        {
            lock(collectionSync)
            {
                var subpath = path.Split(new [] { PathSeparator }, 2);
                subnode = null;
                longestMatching = currentMatching;
                foreach(var currentChild in from.Children)
                {
                    string name;
                    if(!TryGetLocalName(currentChild.Value, out name))
                    {
                        continue;
                    }

                    if(name == subpath[0])
                    {
                        subnode = currentChild;
                        if(subpath.Length == 1)
                        {
                            return true;
                        }
                        return TryFindSubnodeByName(currentChild, subpath[1], out subnode, Subname(currentMatching, subpath[0]), out longestMatching);
                    }
                }
                return false;
            }
        }

        private IPeripheral GetByName(string path)
        {
            IPeripheral result;
            string longestMatching;
            if(!TryGetByName(path, out result, out longestMatching))
            {
                throw new InvalidOperationException(string.Format(
                    "Could not find node '{0}', the longest matching was '{1}'.", path, longestMatching));
            }
            return result;
        }

        private HashSet<IPeripheral> GetParents(IPeripheral child)
        {
            var parents = new HashSet<IPeripheral>();
            registeredPeripherals.TraverseChildrenFirst((parent, children, level) =>
            {
                if(children.Any(x => x.Value.Equals(child)))
                {
                    parents.Add(parent.Value);
                }
            }, 0);
            return parents;
        }

        private ReadOnlyCollection<string> GetNames(IPeripheral peripheral)
        {
            lock(collectionSync)
            {
                var paths = new List<string>();
                if(peripheral == SystemBus)
                {
                    paths.Add(SystemBusName);
                }
                else
                {
                    FindPaths(SystemBusName, peripheral, registeredPeripherals.GetNode(SystemBus), paths);
                }
                return new ReadOnlyCollection<string>(paths);
            }
        }

        private void FindPaths(string nameSoFar, IPeripheral peripheralToFind, MultiTreeNode<IPeripheral, IRegistrationPoint> currentNode, List<string> paths)
        {
            foreach(var child in currentNode.Children)
            {
                var currentPeripheral = child.Value;
                string localName;
                if(!TryGetLocalName(currentPeripheral, out localName))
                {
                    continue;
                }
                var name = Subname(nameSoFar, localName);
                if(currentPeripheral == peripheralToFind)
                {
                    paths.Add(name);
                    return; // shouldn't be attached to itself
                }
                FindPaths(name, peripheralToFind, child, paths);
            }
        }

        private static string Subname(string parent, string child)
        {
            return string.Format("{0}{1}{2}", parent, string.IsNullOrEmpty(parent) ? string.Empty : PathSeparator.ToString(), child);
        }

        private string GetNameForOwnLife(IHasOwnLife ownLife)
        {
            var peripheral = ownLife as IPeripheral;
            if(peripheral != null)
            {
                return GetAnyNameOrTypeName(peripheral);
            }
            return ownLife.ToString();
        }

        private static void DoAttachGPIO(IPeripheral source, PropertyInfo[] gpios, IGPIOReceiver destination, int destinationNumber)
        {
            if(gpios.Length == 0)
            {
                throw new RecoverableException("No GPIO connector found.");
            }
            if(gpios.Length > 1)
            {
                throw new RecoverableException("Ambiguous GPIO connector. Available connectors are: {0}."
                    .FormatWith(gpios.Select(x => x.Name).Aggregate((x, y) => x + ", " + y)));
            }
            (gpios[0].GetValue(source, null) as GPIO).Connect(destination, destinationNumber);
        }

        private static IGPIOReceiver GetActualReceiver(IGPIOReceiver receiver, int? localReceiverNumber)
        {
            var localReceiver = receiver as ILocalGPIOReceiver;
            if(localReceiverNumber.HasValue)
            {
                if(localReceiver != null)
                {
                    return localReceiver.GetLocalReceiver(localReceiverNumber.Value);
                }
                throw new RecoverableException("The specified receiver does not support localReceiverNumber.");
            }
            return receiver;
        }

        private void ReportForeignEventInner(Action<long, bool> recordMethod, Action handlerMethod)
        {
            if(syncDomain.OnSyncPointThread)
            {
                handlerMethod();
                if(recorder != null)
                {
                    // it came from somebody in our domain, like synchronized router
                    recordMethod(SyncDomain.SynchronizationsCount, false);
                }
                return;
            }
            syncDomain.ExecuteOnNearestSync(() =>
            { 
                handlerMethod();
                if(recorder != null)
                {
                    recordMethod(SyncDomain.SynchronizationsCount, true);
                }
            });
        }

        private void RegisterManagedThread(ManagedThread managedThread)
        {
            lock(collectionSync)
            {
                ownLifes.Add(managedThread);
            }
        }

        private void CollectGarbageStamp()
        {
            currentStampLevel++;
            if(currentStampLevel != 1)
            {
                return;
            }
            currentStamp = new List<IPeripheral>();
            registeredPeripherals.TraverseParentFirst((peripheral, level) => currentStamp.Add(peripheral), 0);
        }

        private void CollectGarbage()
        {
            currentStampLevel--;
            if(currentStampLevel != 0)
            {
                return;
            }
            var toDelete = currentStamp.Where(x => !IsRegistered(x)).ToArray();
            DetachIncomingInterrupts(toDelete);
            DetachOutgoingInterrupts(toDelete);
            foreach(var value in toDelete)
            {
                ((PeripheralsGroupsManager)PeripheralsGroups).RemoveFromAllGroups(value);
                var ownLife = value as IHasOwnLife;
                if(ownLife != null)
                {
                    ownLifes.Remove(ownLife);
                }
                // we may also need to remove managed threads used by the device
                // note that if peripheral is put on shelf, its managed threads are
                // already removed from ownLifes
                var dependantThreads = ownLifes.OfType<ManagedThread>().Where(x => x.Owner == value).ToArray();
                foreach(var thread in dependantThreads)
                {
                    ownLifes.Remove(thread);
                }
                EmulationManager.Instance.CurrentEmulation.Connector.DisconnectFromAll(value);

                localNames.Remove(value);
                var disposable = value as IDisposable;
                if(disposable != null)
                {
                    disposable.Dispose();
                }
            }
            currentStamp = null;
        }

        private void DetachIncomingInterrupts(IPeripheral[] detachedPeripherals)
        {
            foreach(var detachedPeripheral in detachedPeripherals)
            {
                // find all peripherials' GPIOs and check which one is connected to detachedPeripherial
                foreach(var peripheral in registeredPeripherals.Children.Select(x => x.Value).Distinct())
                {
                    foreach(var gpio in peripheral.GetGPIOs().Select(x => x.Item2))
                    {
                        if(gpio.Endpoint != null && gpio.Endpoint.Receiver == detachedPeripheral)
                        {
                            gpio.Disconnect();
                        }
                    }
                }
            }
        }

        private static void DetachOutgoingInterrupts(IEnumerable<IPeripheral> peripherals)
        {
            foreach(var peripheral in peripherals)
            {
                foreach(var gpio in peripheral.GetGPIOs().Select(x => x.Item2))
                {
                    gpio.Disconnect();
                }
            }
        }

        private void Resume()
        {
            stopwatch.Start();
            lock(pausingSync)
            {
                currentSynchronizer.RestoreSync();
                foreach(var ownLife in ownLifes.OrderBy(x => x is ICPU ? 1 : 0))
                {
                    this.NoisyLog("Resuming {0}.", GetNameForOwnLife(ownLife));
                    ownLife.Resume();
                }
                hostTimeClockSource.Resume();
                this.Log(LogLevel.Info, "Emulation resumed.");
                state = State.Started;
                var machineStarted = StateChanged;
                if(machineStarted != null)
                {
                    machineStarted(this, new MachineStateChangedEventArgs(MachineStateChangedEventArgs.State.Started));
                }
            }
        }

        /// <summary>
        /// This function is only used as an ID for clock source.
        /// </summary>
        private void IndicatorForClockSource()
        {
        }

        private void SynchronizeInDomain()
        {
            if(!currentSynchronizer.Sync())
            {
                return;
            }
            // one can see that managed threads are executed here which means they are synchronized only with regards to
            // the given machine, not to all machines
            // this is perfectly ok since cpu thread can also do anything with regards to other machines, their
            // synchronization is guaranteed by synchronized externals, like switches etc
            foreach(var syncedManagedThread in syncedManagedThreads)
            {
                syncedManagedThread.RunOnce();
            }
            var delayedTasksCount = delayedTasks.Count;
            if(delayedTasksCount == 0)
            {
                return;
            }

            timeSpanBySyncSoFar += TimeSpan.FromTicks(Antmicro.Renode.Time.Consts.TimeQuantum.Ticks * (long)SyncUnit);
            // we'll run all tasks that are late
            var tasksToExecute = delayedTasks.GetViewBetween(DelayedTask.Zero, new DelayedTask(null, timeSpanBySyncSoFar));
            var tasksAsArray = tasksToExecute.ToArray();
            tasksToExecute.Clear();
            foreach(var task in tasksAsArray)
            {
                task.What();
            }
        }

        private string userState;
        private Action<string> userStateHook;
        private bool alreadyDisposed;
        private State state;
        private PausedState pausedState;
        private List<IPeripheral> currentStamp;
        private int currentStampLevel;
        private ISynchronizer currentSynchronizer;
        private ISynchronizationDomain syncDomain;
        private Recorder recorder;
        private Player player;
        private DateTime machineStartedAt;
        private TimeSpan timeSpanBySyncSoFar;
        private readonly MultiTree<IPeripheral, IRegistrationPoint> registeredPeripherals;
        private readonly Dictionary<IPeripheral, string> localNames;
        private readonly HashSet<IHasOwnLife> ownLifes;
        private readonly HostTimeClockSource hostTimeClockSource;
        private readonly Stopwatch stopwatch;
        private readonly ClockSourceWrapper clockSourceWrapper;
        private readonly List<SynchronizedManagedThread> syncedManagedThreads;
        private readonly SortedSet<DelayedTask> delayedTasks;
        private readonly object collectionSync;
        private readonly object pausingSync;
        private readonly object disposedSync;

        private const string NoShelfForUnnamed = "Peripheral must be named in order to be put on shelf";
        private const long DefaultSyncUnit = 10000;

        private enum State
        {
            NotStarted,
            Started,
            Paused
        }

        private sealed class ClockSourceWrapper : IClockSource
        {
            public ClockSourceWrapper(IClockSource firstClockSource, Machine machine)
            {
                this.machine = machine;
                currentClockSource = firstClockSource;
            }

            public void ExecuteInLock(Action action)
            {
                CurrentClockSource.ExecuteInLock(action);
            }

            public void AddClockEntry(ClockEntry entry)
            {
                CurrentClockSource.AddClockEntry(entry);
            }

            public ClockEntry GetClockEntry(Action action)
            {
                return CurrentClockSource.GetClockEntry(action);
            }

            public void GetClockEntryInLockContext(Action action, Action<ClockEntry> visitor)
            {
                CurrentClockSource.GetClockEntryInLockContext(action, visitor);
            }

            public IEnumerable<ClockEntry> GetAllClockEntries()
            {
                return CurrentClockSource.GetAllClockEntries();
            }

            public bool RemoveClockEntry(Action action)
            {
                return CurrentClockSource.RemoveClockEntry(action);
            }

            public void ExchangeClockEntryWith(Action action, Func<ClockEntry, ClockEntry> factory,
                                               Func<ClockEntry> factorIfNonExistant = null)
            {
                CurrentClockSource.ExchangeClockEntryWith(action, factory, factorIfNonExistant);
            }

            public ulong CurrentValue
            {
                get
                {
                    return CurrentClockSource.CurrentValue;
                }
            }

            public IEnumerable<ClockEntry> EjectClockEntries()
            {
                return CurrentClockSource.EjectClockEntries();
            }

            public void AddClockEntries(IEnumerable<ClockEntry> entries)
            {
                CurrentClockSource.AddClockEntries(entries);
            }

            public IClockSource CurrentClockSource
            {
                get
                {
                    return currentClockSource;
                }
                set
                {
                    using(machine.ObtainPausedState())
                    {
                        var entries = currentClockSource.EjectClockEntries();
                        currentClockSource = value;
                        currentClockSource.AddClockEntries(entries);
                    }
                }
            }

            private IClockSource currentClockSource;
            private readonly Machine machine;

        }

        private abstract class ManagedThreadBase
        {
            public override string ToString()
            {
                var ownerAsPeripheral = Owner as IPeripheral;
                string ownerName;
                ownerName = ownerAsPeripheral != null ? Machine.GetAnyNameOrTypeName(ownerAsPeripheral) : Owner.ToString();
                var result = Name == null ? ownerName : string.Format("{0}: {1}", ownerName, Name);
                if(Frequency != 0)
                {
                    result = string.Format("{0} ({1}Hz)", result, Frequency);
                }
                return result;
            }

            public object Owner { get; private set; }

            protected ManagedThreadBase(Action action, object owner, Machine machine, string name, uint frequency)
            {
                this.Machine = machine;
                Name = name;
                ThreadAction = action;
                Frequency = frequency;
                Owner = owner;
                if(frequency != 0)
                {
                    TimePerRun = TimeSpan.FromSeconds(1.0 / frequency);
                }
            }

            protected readonly Action ThreadAction;
            protected readonly uint Frequency;
            protected readonly string Name;
            protected readonly TimeSpan TimePerRun;
            protected readonly Machine Machine;
        }

        private sealed class ManagedThread : ManagedThreadBase, IManagedThread, IHasOwnLife
        {
            public ManagedThread(Action action, object owner, Machine machine, string name, uint frequency = 0)
                : base(action, owner, machine, name, frequency)
            {
                sync = new object();
                paused = true;

            }

            public void Start()
            {
                ChangeState(activeTo: true);
            }

            public void Stop()
            {
                ChangeState(activeTo: false);
            }

            void IHasOwnLife.Start()
            {
                ChangeState(pausedTo: false);
            }

            void IHasOwnLife.Pause()
            {
                ChangeState(pausedTo: true);
            }

            void IHasOwnLife.Resume()
            {
                ChangeState(pausedTo: false);
            }

            private void ChangeState(bool? activeTo = null, bool? pausedTo = null)
            {
                Thread localThread;
                lock(sync)
                {
                    localThread = thread;
                    var wasStarted = active && !paused;
                    var willBeStarted = (activeTo ?? active) && !(pausedTo ?? paused);
                    active = activeTo ?? active;
                    paused = pausedTo ?? paused;
                    if(wasStarted == willBeStarted)
                    {
                        return;
                    }
                    if(willBeStarted)
                    {
                        thread = new Thread(InternalRun) {
                            IsBackground = true,
                            Name = Name
                        };
                        thread.Start();
                        return;
                    }
                }
                // we can't wait in lock for the thread to be ended, it may check the stop condition ;)
                // also if we're in the same thread we started, we won't wait
                if(localThread.ManagedThreadId == Thread.CurrentThread.ManagedThreadId)
                {
                    return;
                }
                // otherwise wait for thread to be stopped
                localThread.Join();
            }

            private void InternalRun()
            {
                while(true)
                {
                    lock(sync)
                    {
                        if(!active || paused)
                        {
                            break;
                        }
                    }
                    lastRun = CustomDateTime.Now;
                    ThreadAction();
                    if(Frequency == 0)
                    {
                        continue;
                    }
                    // sleep adequately to sustain desired frequency
                    var now = CustomDateTime.Now;
                    var diff = now - lastRun;
                    if(diff > TimePerRun)
                    {
                        continue;
                    }
                    Thread.Sleep(TimePerRun - diff);
                }
            }

            [Transient]
            private Thread thread;

            private bool active;
            private bool paused;
            private DateTime lastRun;
            private readonly object sync;
        }

        private sealed class SynchronizedManagedThread : ManagedThreadBase, IManagedThread
        {
            public SynchronizedManagedThread(Action action, object owner, Machine machine, string name, uint frequency)
                : base(action, owner, machine, name, frequency)
            {
                if(frequency == 0)
                {
                    throw new InvalidOperationException("Frequency of the synchronized thread cannot be zero.");
                }
            }

            public void RunOnce()
            {
                if(!active)
                {
                    return;
                }
                var now = Machine.ElapsedVirtualTime;
                var diff = now - lastRun;
                if(diff < TimePerRun)
                {
                    return;
                }
                lastRun = now;
                ThreadAction();
            }

            public void Start()
            {
                active = true;
            }

            public void Stop()
            {
                active = false;
            }

            private TimeSpan lastRun;
            private bool active;
        }

        private sealed class PausedState : IDisposable
        {
            public PausedState(Machine machine)
            {
                this.machine = machine;
                sync = new object();
            }

            public PausedState Enter()
            {
                LevelUp();
                return this;
            }

            public void Exit()
            {
                LevelDown();
            }

            public void Dispose()
            {
                Exit();
            }

            private void LevelUp()
            {
                lock(sync)
                {
                    if(currentLevel == 0)
                    {
                        if(machine.IsPaused)
                        {
                            wasPaused = true;
                        }
                        else
                        {
                            wasPaused = false;
                            machine.Pause();
                        }
                    }
                    currentLevel++;
                }
            }

            private void LevelDown()
            {
                lock(sync)
                {
                    if(currentLevel == 1)
                    {
                        if(!wasPaused)
                        {
                            machine.Start();
                        }
                    }
                    if(currentLevel == 0)
                    {
                        throw new InvalidOperationException("LevelDown without prior LevelUp");
                    }
                    currentLevel--;
                }
            }

            private int currentLevel;
            private bool wasPaused;
            private readonly Machine machine;
            private readonly object sync;
        }

        private struct DelayedTask : IComparable<DelayedTask>
        {
            static DelayedTask()
            {
                Zero = new DelayedTask();
            }

            public DelayedTask(Action what, TimeSpan when) : this()
            {
                What = what;
                When = when;
                id = Interlocked.Increment(ref Id);
            }

            public int CompareTo(DelayedTask other)
            {
                var result = When.CompareTo(other.When);
                return result != 0 ? result : id.CompareTo(other.id);
            }

            public Action What { get; private set; }

            public TimeSpan When { get; private set; }

            public static DelayedTask Zero { get; private set; }

            private readonly int id;
            private static int Id;
        }

        private sealed class PeripheralsGroupsManager : IPeripheralsGroupsManager
        {
            public PeripheralsGroupsManager(Machine machine)
            {
                this.machine = machine;
                groups = new List<PeripheralsGroup>();
            }

            public IPeripheralsGroup GetOrCreate(string name, IEnumerable<IPeripheral> peripherals)
            {
                IPeripheralsGroup existingResult = null;
                var result = (PeripheralsGroup)existingResult;
                if(!TryGetByName(name, out existingResult))
                {
                    result = new PeripheralsGroup(name, machine);
                    groups.Add(result);
                }

                foreach(var p in peripherals)
                {
                    result.Add(p);
                }

                return result;
            }

            public IPeripheralsGroup GetOrCreate(string name)
            {
                IPeripheralsGroup result;
                if(!TryGetByName(name, out result))
                {
                    result = new PeripheralsGroup(name, machine);
                    groups.Add((PeripheralsGroup)result);
                }

                return result;
            }

            public void RemoveFromAllGroups(IPeripheral value)
            {
                foreach(var group in ActiveGroups)
                {
                    ((List<IPeripheral>)group.Peripherals).Remove(value);
                }
            }

            public bool TryGetActiveGroupContaining(IPeripheral peripheral, out IPeripheralsGroup group)
            {
                group = ActiveGroups.SingleOrDefault(x => ((PeripheralsGroup)x).Contains(peripheral));
                return group != null;
            }

            public bool TryGetAnyGroupContaining(IPeripheral peripheral, out IPeripheralsGroup group)
            {
                group = groups.SingleOrDefault(x => x.Contains(peripheral));
                return group != null;
            }

            public bool TryGetByName(string name, out IPeripheralsGroup group)
            {
                group = ActiveGroups.SingleOrDefault(x => x.Name == name);
                return group != null;
            }

            public IEnumerable<IPeripheralsGroup> ActiveGroups
            { 
                get
                {
                    return groups.Where(x => x.IsActive);
                }
            }

            private readonly List<PeripheralsGroup> groups;
            private readonly Machine machine;

            private sealed class PeripheralsGroup : IPeripheralsGroup
            {
                public PeripheralsGroup(string name, Machine machine)
                {
                    Machine = machine;
                    Name = name;
                    IsActive = true;
                    Peripherals = new List<IPeripheral>();
                }

                public void Add(IPeripheral peripheral)
                {
                    if(!Machine.IsRegistered(peripheral))
                    {
                        throw new RegistrationException("Peripheral must be registered prior to adding to the group");
                    }
                    ((List<IPeripheral>)Peripherals).Add(peripheral);
                }

                public bool Contains(IPeripheral peripheral)
                {
                    return Peripherals.Contains(peripheral);
                }

                public void Remove(IPeripheral peripheral)
                {
                    ((List<IPeripheral>)Peripherals).Remove(peripheral);
                }

                public void Unregister()
                {
                    IsActive = false;
                    using(Machine.ObtainPausedState())
                    {
                        foreach(var p in Peripherals.ToList())
                        {
                            Machine.UnregisterFromParent(p);
                        }
                    }
                    ((PeripheralsGroupsManager)Machine.PeripheralsGroups).groups.Remove(this);
                }

                public string Name { get; private set; }

                public bool IsActive { get; private set; }

                public Machine Machine { get; private set; }

                public IEnumerable<IPeripheral> Peripherals { get; private set; }
            }
        }
    }
}

