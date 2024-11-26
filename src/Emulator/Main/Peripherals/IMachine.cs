//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2023 Western Digital Corporation
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.EventRecording;
using Antmicro.Renode.Logging.Profiling;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Core
{
    public interface IMachine: IEmulationElement
    {
        void AddUserStateHook(Func<string, bool> predicate, Action<string> hook);
        void AppendDirtyAddresses(ICPU cpu, long[] addresses);
        void AttachGPIO(IPeripheral source, int sourceNumber, IGPIOReceiver destination, int destinationNumber, int? localReceiverNumber = null);
        void AttachGPIO(IPeripheral source, IGPIOReceiver destination, int destinationNumber, int? localReceiverNumber = null);
        void AttachGPIO(IPeripheral source, string connectorName, IGPIOReceiver destination, int destinationNumber, int? localReceiverNumber = null);
        void CheckRecorderPlayer();
        void EnableGdbLogging(int port, bool enabled);
        void EnableProfiler(string outputPath = null);
        IEnumerable<string> GetAllNames();
        string GetAnyNameOrTypeName(IPeripheral peripheral);
        IEnumerable<IPeripheral> GetChildrenPeripherals(IPeripheral peripheral);
        string[,] GetClockSourceInfo();
        string GetLocalName(IPeripheral peripheral);
        long[] GetNewDirtyAddressesForCore(ICPU cpu);
        IEnumerable<IPeripheral> GetParentPeripherals(IPeripheral peripheral);
        IEnumerable<IRegistrationPoint> GetPeripheralRegistrationPoints(IPeripheral parentPeripheral, IPeripheral childPeripheral);
        IEnumerable<T> GetPeripheralsOfType<T>();
        IEnumerable<IPeripheral> GetPeripheralsOfType(Type t);
        IEnumerable<PeripheralTreeEntry> GetRegisteredPeripherals();
        IBusController GetSystemBus(IBusPeripheral peripheral);
        void HandleTimeDomainEvent<T>(Action<T> handler, T handlerArgument, TimeStamp eventTime, Action postAction = null);
        void HandleTimeDomainEvent<T1, T2>(Action<T1, T2> handler, T1 handlerArgument1, T2 handlerArgument2, TimeStamp eventTime, Action postAction = null);
        void HandleTimeDomainEvent<T>(Action<T> handler, T handlerArgument, bool timeDomainInternalEvent);
        void HandleTimeDomainEvent<T1, T2>(Action<T1, T2> handler, T1 handlerArgument1, T2 handlerArgument2, bool timeDomainInternalEvent);
        void HandleTimeProgress(TimeInterval diff);
        void InitAtomicMemoryState();
        bool IsRegistered(IPeripheral peripheral);
        IManagedThread ObtainManagedThread(Action action, uint frequency, string name = "managed thread", IEmulationElement owner = null, Func<bool> stopCondition = null);
        IManagedThread ObtainManagedThread(Action action, TimeInterval period, string name = "managed thread", IEmulationElement owner = null, Func<bool> stopCondition = null);
        IDisposable ObtainPausedState(bool internalPause = false);
        void Pause();
        void PauseAndRequestEmulationPause(bool precise = false);
        void PlayFrom(ReadFilePath fileName);
        void PostCreationActions();
        void RecordTo(string fileName, RecordingBehaviour recordingBehaviour);
        void RegisterAsAChildOf(IPeripheral peripheralParent, IPeripheral peripheralChild, IRegistrationPoint registrationPoint);
        IBusController RegisterBusController(IBusPeripheral peripheral, IBusController controller);
        void RequestReset();
        void RequestResetInSafeState(Action postReset = null, ICollection<IPeripheral> unresetable = null);
        void Reset();
        void ScheduleAction(TimeInterval delay, Action<TimeInterval> action, string name = null);
        void SetLocalName(IPeripheral peripheral, string name);
        void Start();
        void StartGdbServer(int port, bool autostartEmulation = true, string cpuCluster = "");
        void StartGdbServer(int port, bool autostartEmulation, ICluster<ICpuSupportingGdb> cpu);
        void StopGdbServer(int? port = null);
        bool AttachConnectionAcceptedListenerToGdbStub(int port, Action<System.IO.Stream> listener);
        bool DetachConnectionAcceptedListenerFromGdbStub(int port, Action<System.IO.Stream> listener);
        bool IsGdbConnectedToServer(int port);
        string ToString();
        bool TryGetAnyName(IPeripheral peripheral, out string name);
        bool TryGetBusController(IBusPeripheral peripheral, out IBusController controller);
        bool TryGetByName<T>(string name, out T peripheral, out string longestMatch) where T : class, IPeripheral;
        bool TryGetByName<T>(string name, out T peripheral) where T : class, IPeripheral;
        bool TryGetLocalName(IPeripheral peripheral, out string name);
        bool TryRestartTranslationBlockOnCurrentCpu(bool quiet = false);
        void UnregisterAsAChildOf(IPeripheral peripheralParent, IPeripheral peripheralChild);
        void UnregisterAsAChildOf(IPeripheral peripheralParent, IRegistrationPoint registrationPoint);
        void UnregisterFromParent(IPeripheral peripheral);
        void ExchangeRegistrationPointForPeripheral(IPeripheral parent, IPeripheral child, IRegistrationPoint oldPoint, IRegistrationPoint newPoint);

        IPeripheral this[string name] { get; }
        IntPtr AtomicMemoryStatePointer { get; }
        IClockSource ClockSource { get; }
        bool HasRecorder { get; }
        bool HasPlayer { get; }
        Profiler Profiler { get; }
        string UserState { get; set; }
        IBusController SystemBus { get; }
        IPeripheralsGroupsManager PeripheralsGroups { get; }
        Platform Platform { get; set; }
        bool IsPaused { get; }
        TimeStamp ElapsedVirtualTime { get; }
        TimeSourceBase LocalTimeSource { get; set; }
        DateTime RealTimeClockDateTime { get; }
        RealTimeClockMode RealTimeClockMode { get; set; }
        DateTime RealTimeClockStart { get; }
        bool InternalPause { get; }
        bool IgnorePeripheralRegistrationConditions { get; set; }

        event Action<IMachine> MachineReset;
        event Action<IMachine, PeripheralsChangedEventArgs> PeripheralsChanged;
        event Action<IMachine> RealTimeClockModeChanged;
        event Action<IMachine, MachineStateChangedEventArgs> StateChanged;
    }
}

