//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Antmicro.Migrant;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Time;
using Antmicro.Renode.UserInterface.Tokenizer;

using AntShell.Commands;

namespace Antmicro.Renode.UserInterface.Commands
{
    public class SetAndRevertAfterCommand : Command
    {
        public SetAndRevertAfterCommand(Monitor monitor) : base(monitor, "setAndRevertAfter", "Sets value for a period of time then restores previous value", "sara")
        {
            EmulationManager.Instance.EmulationChanged += () =>
            {
                externalsHandler = null;
                handlers.Clear();
                EmulationManager.Instance.CurrentEmulation.MachineRemoved += RemoveDomain;
            };
            EmulationManager.Instance.CurrentEmulation.MachineRemoved += RemoveDomain;
        }

        public override void PrintHelp(ICommandInteraction writer)
        {
            base.PrintHelp(writer);
            writer.WriteLine("Usage:");
            writer.WriteLine($"\t{Name} TIME DEVICE PROPERTY VALUE");
        }

        [Runnable]
        public void Run(ICommandInteraction writer, DecimalIntegerToken interval, LiteralToken deviceToken, params Token[] tokensArray)
        {
            Run(writer, (TimeIntervalToken)interval, deviceToken, tokensArray);
        }

        [Runnable]
        public void Run(ICommandInteraction writer, FloatToken interval, LiteralToken deviceToken, params Token[] tokensArray)
        {
            Run(writer, (TimeIntervalToken)interval, deviceToken, tokensArray);
        }

        [Runnable]
        public void Run(ICommandInteraction writer, TimeIntervalToken interval, LiteralToken deviceToken, params Token[] tokensArray)
        {
            var saveableCommand = default(SerializableCommandInvocation);
            var value = default(object);

            try
            {
                saveableCommand = new SerializableCommandInvocation(monitor, deviceToken, tokensArray, out value);
            }
            catch(InvalidOperationException e)
            {
                writer.WriteError(e.Message);
                return;
            }
            catch(ArgumentException e)
            {
                writer.WriteError(e.Message);
                PrintHelp(writer);
                return;
            }

            QueueRevert(saveableCommand, interval.Value);

            // set new value
            DeviceHelper.InvokeSet(saveableCommand.Device, saveableCommand.MemberInfo, value);
        }

        private static Type GetAccessorType(MemberInfo info)
        {
            if(info is PropertyInfo pInfo)
            {
                return pInfo.PropertyType;
            }
            else if(info is FieldInfo fInfo)
            {
                return fInfo.FieldType;
            }
            throw new ArgumentException("Passed member is not an accessor", nameof(info));
        }

        private void QueueRevert(SerializableCommandInvocation command, TimeInterval offset)
        {
            Handler handler;

            var domain = command.Machine;
            if(domain == null)
            {
                externalsHandler = externalsHandler ?? new ExternalsHandler();
                handler = externalsHandler;
            }
            else
            {
                if(!handlers.TryGetValue(domain, out var domainHandler))
                {
                    domainHandler = new DomainHandler(domain);
                    handlers.Add(domain, domainHandler);
                }
                handler = domainHandler;
            }

            handler.QueueRevert(command, offset);
        }

        private void RemoveDomain(IMachine machine)
        {
            if(handlers.Remove(machine))
            {
                handlers.Remove(machine);
            }
        }

        private ExternalsHandler externalsHandler;
        private readonly IDictionary<IMachine, DomainHandler> handlers = new Dictionary<IMachine, DomainHandler>();

        private class DomainHandler : Handler
        {
            public DomainHandler(IMachine machine) : base()
            {
                this.machine = machine;
            }

            public override void QueueRevert(SerializableCommandInvocation command, TimeInterval offset)
            {
                machine.ClockSource.ExecuteInLock(() =>
                {
                    base.QueueRevert(command, offset);
                });
            }

            protected override void UpdateSchedule(TimeInterval? maybeInterval)
            {
                if(maybeInterval is TimeInterval interval)
                {
                    machine.ClockSource.ExchangeClockEntryWith(Update,
                        entry => entry.With(period: interval.Ticks, enabled: true),
                        () => new ClockEntry(
                            period: interval.Ticks,
                            frequency: TimeInterval.TicksPerSecond,
                            // clock entry handler is executed in lock, so there is no need to wrap Update
                            handler: Update,
                            owner: machine,
                            localName: $"{nameof(SetAndRevertAfterCommand)}",
                            enabled: true,
                            workMode: WorkMode.OneShot
                        )
                    );
                }
                else
                {
                    machine.ClockSource.TryRemoveClockEntry(Update);
                }
            }

            protected override TimeInterval GetCurrentTime()
            {
                return machine.ClockSource.CurrentValue;
            }

            private readonly IMachine machine;
        }

        private class ExternalsHandler : Handler
        {
            public ExternalsHandler() : base()
            {
                emulation = EmulationManager.Instance.CurrentEmulation;
                emulation.MasterTimeSource.TimePassed += _ => Update();
            }

            public override void QueueRevert(SerializableCommandInvocation command, TimeInterval offset)
            {
                lock(locker)
                {
                    base.QueueRevert(command, offset);
                }
            }

            protected override TimeInterval GetCurrentTime()
            {
                return emulation.MasterTimeSource.ElapsedVirtualTime;
            }

            protected override void Update()
            {
                lock(locker)
                {
                    base.Update();
                }
            }

            private readonly object locker = new object();
            private readonly Emulation emulation;
        }

        private class SerializableCommandInvocation
        {
            public SerializableCommandInvocation(Monitor monitor, LiteralToken deviceToken, IEnumerable<Token> tokens, out object value)
            {
                this.tokens = tokens;
                this.monitor = monitor;
                this.monitorContext = Serializer.DeepClone(monitor.MonitorContext);

                if(!monitor.DeviceHelpers.IsNameAvailable(deviceToken.Value))
                {
                    throw new InvalidOperationException($"No device found: {deviceToken.Value}");
                }
                if(!Monitor.TryFindPeripheralTypeByName(deviceToken.Value, out _, out var longestMatch, out actualName))
                {
                    throw new InvalidOperationException($"Could not find device {deviceToken.Value}, the longest match is {longestMatch}");
                }

                GetDevice(out chainedName, out var tailTokens);
                var firstToken = tailTokens?.FirstOrDefault();
                if(!(firstToken is LiteralToken))
                {
                    throw new ArgumentException($"No accessor specified for {chainedName}");
                }

                memberToken = firstToken as LiteralToken;
                tailTokens = tailTokens.Skip(1);

                var i = 0;
                if(!monitor.DeviceHelpers.ParseArgument(tailTokens.ToArray(), ref i, out var arg))
                {
                    throw new InvalidOperationException($"Argument not found for {chainedName} {memberToken.Value}");
                }
                tailTokens = tailTokens.Skip(i + 1);

                if(tailTokens.Any())
                {
                    throw new ArgumentException($"Too many arguments passed");
                }

                var memberType = GetAccessorType(MemberInfo);
                if(!monitor.DeviceHelpers.FitArgumentType(arg, memberType, out value))
                {
                    throw new InvalidOperationException($"Could not convert {arg} to {memberType}");
                }
            }

            public override string ToString() => $"<{Device}>.<{MemberInfo}>";

            public bool SameTarget(SerializableCommandInvocation other) =>
                Device == other.Device && MemberInfo == other.MemberInfo;

            public object Device => device ?? (device = GetDevice(out _, out _));

            public MemberInfo MemberInfo
            {
                get
                {
                    if(member == null)
                    {
                        MemberInfo info;
                        using(Monitor.EnterContext(monitorContext))
                        {
                            info = Monitor.DeviceHelpers.GetAccessor(Device, memberToken.Value, assertSetter: true, assertGetter: true);
                        }

                        if(info == null)
                        {
                            throw new RecoverableException($"Accessor '{memberToken.Value}' not found for {chainedName}");
                        }

                        member = info;
                    }

                    return member;
                }
            }

            public IMachine Machine => monitorContext.CurrentMachine;

            private object GetDevice(out string chainedName, out IEnumerable<Token> tail)
            {
                using(Monitor.EnterContext(monitorContext))
                {
                    var rootDevice = Monitor.DeviceHelpers.IdentifyDevice(actualName);
                    device = Monitor.DeviceHelpers.HandleDeviceChain(actualName, out chainedName, rootDevice, tokens, out tail);
                }
                return device;
            }

            private Monitor Monitor => monitor ?? (monitor = ObjectCreator.Instance.GetSurrogate<Monitor>());

            [Transient]
            private Monitor monitor;

            [Transient]
            private object device;
            [Transient]
            private MemberInfo member;

            private readonly MonitorContext monitorContext;

            private readonly IEnumerable<Token> tokens;
            private readonly LiteralToken memberToken;

            private readonly string actualName;
            private readonly string chainedName;
        }

        private abstract class Handler
        {
            public virtual void QueueRevert(SerializableCommandInvocation command, TimeInterval offset)
            {
                var now = GetCurrentTime();
                var ts = now + offset;

                var revert = new Revert(ts, command);

                var node = reverts.First;
                while(node != null && node.Value.Timestamp <= ts)
                {
                    var next = node.Next;
                    if(node.Value.Command.SameTarget(revert.Command))
                    {
                        if(node.Value.Timestamp == ts)
                        {
                            this.Trace($"sara: Revert {node.Value} already exists");
                            return;
                        }
                        revert.Value = node.Value.Value;
                        this.Trace($"sara: Dropping {node.Value}");
                        reverts.Remove(node);
                    }
                    node = next;
                }

                revert.Value = revert.Value ?? DeviceHelper.InvokeGet(command.Device, command.MemberInfo);
                this.Trace($"sara: Pushing {revert}");
                if(node == null)
                {
                    reverts.AddLast(revert);
                }
                else
                {
                    reverts.AddBefore(node, revert);
                }

                this.Trace($"sara: Next update {reverts.First.Value}");
                UpdateSchedule(reverts.First.Value.Timestamp - now);
            }

            protected virtual void Update()
            {
                var now = GetCurrentTime();
                var node = reverts.First;
                while(node != null && node.Value.Timestamp <= now)
                {
                    this.Trace($"sara: Executing {node.Value}");
                    DeviceHelper.InvokeSet(node.Value.Command.Device, node.Value.Command.MemberInfo, node.Value.Value);
                    reverts.RemoveFirst();
                    node = reverts.First;
                }
                this.Trace($"sara: Next update {reverts.First?.Value.ToString() ?? "not scheduled"}");
                UpdateSchedule(reverts.First?.Value.Timestamp - now ?? null);
            }

            protected virtual void UpdateSchedule(TimeInterval? _)
            {
                // Intentionally left empty
            }

            protected abstract TimeInterval GetCurrentTime();

            // NOTE: Kept in order by Revert.Timestamp
            private readonly LinkedList<Revert> reverts = new LinkedList<Revert>();

            protected struct Revert
            {
                public Revert(TimeInterval timestamp, SerializableCommandInvocation command)
                {
                    Timestamp = timestamp;
                    Command = command;
                    Value = null;
                }

                public override string ToString() => $"{{{Command} := {Value} @ {Timestamp}}}";

                public readonly TimeInterval Timestamp;
                public readonly SerializableCommandInvocation Command;
                public object Value;
            }
        }
    }
}
