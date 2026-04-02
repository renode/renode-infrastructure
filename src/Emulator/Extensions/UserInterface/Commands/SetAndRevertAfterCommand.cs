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

using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Time;
using Antmicro.Renode.UserInterface.Tokenizer;
using Antmicro.Renode.Utilities.Collections;

using AntShell.Commands;

namespace Antmicro.Renode.UserInterface.Commands
{
    public class SetAndRevertAfterCommand : Command
    {
        public SetAndRevertAfterCommand(Monitor monitor, Monitor.DeviceHandlingHelpers helpers) : base(monitor, "setAndRevertAfter", "Sets value for a period of time then restores previous value", "sara")
        {
            this.helpers = helpers;
            monitor.CacheCleared += cache.ClearCache;
            EmulationManager.Instance.EmulationChanged += () =>
            {
                externalsHandler = null;
                handlers.Clear();
                cache.ClearCache();
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
        public void For(ICommandInteraction writer, DecimalIntegerToken interval, LiteralToken deviceToken, params Token[] tokensArray)
        {
            For(writer, (TimeIntervalToken)interval, deviceToken, tokensArray);
        }

        [Runnable]
        public void For(ICommandInteraction writer, FloatToken interval, LiteralToken deviceToken, params Token[] tokensArray)
        {
            For(writer, (TimeIntervalToken)interval, deviceToken, tokensArray);
        }

        [Runnable]
        public void For(ICommandInteraction writer, TimeIntervalToken interval, LiteralToken deviceToken, params Token[] tokensArray)
        {
            var tokens = tokensArray.AsEnumerable();

            // get root device
            if(!helpers.IsNameAvailable(deviceToken.Value))
            {
                writer.WriteError($"No device found: {deviceToken.Value}");
                return;
            }
            if(!monitor.TryFindPeripheralTypeByName(deviceToken.Value, out var deviceType, out var longestMatch, out var actualName))
            {
                writer.WriteError($"Could not find device {deviceToken.Value}, the longest match is {longestMatch}");
                return;
            }
            var device = helpers.IdentifyDevice(actualName);

            // get machine if available
            IMachine machine = null;
            (device as IPeripheral)?.TryGetMachine(out machine);

            // get actual device
            device = helpers.HandleDeviceChain(actualName, out var chainedName, device, tokens, out tokens);

            // get accessor
            if(!(tokens?.FirstOrDefault() is LiteralToken memberToken))
            {
                writer.WriteError($"No accessor specified for {chainedName}");
                PrintHelp(writer);
                return;
            }

            MemberInfo memberInfo = null;
            try
            {
                memberInfo = cache.Get(device, memberToken.Value, this.GetAccessor);
            }
            catch(RecoverableException e)
            {
                writer.WriteError($"Accessor {e.Message}");
                return;
            }
            if(memberInfo == null)
            {
                writer.WriteError($"Accessor '{memberToken.Value}' not found for {chainedName}");
                return;
            }
            tokens = tokens.Skip(1);

            // get and validate new value
            var i = 0;
            if(!helpers.ParseArgument(tokens.ToArray(), ref i, out var arg))
            {
                writer.WriteError($"Argument not found for {chainedName} {memberToken.Value}");
                return;
            }
            tokens = tokens.Skip(i + 1);

            if(tokens.Any())
            {
                writer.WriteError($"Too many arguments passed");
                PrintHelp(writer);
                return;
            }

            var memberType = GetAccessorType(memberInfo);
            if(!helpers.FitArgumentType(arg, memberType, out var value))
            {
                writer.WriteError($"Could not convert {arg} to {memberType}");
                return;
            }

            // queue revert
            QueueRevert(machine, device, memberInfo, interval.Value);

            // set new value
            DeviceHelper.InvokeSet(device, memberInfo, value);
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

        private MemberInfo GetAccessor(object device, string member)
            => helpers.GetAccessor(device, member, assertSetter: true, assertGetter: true);

        private void QueueRevert(IMachine domain, object device, MemberInfo member, TimeInterval offset)
        {
            Handler handler;
            if(domain == null)
            {
                externalsHandler = externalsHandler ?? new ExternalsHandler(this);
                handler = externalsHandler;
            }
            else
            {
                if(!handlers.TryGetValue(domain, out var domainHandler))
                {
                    domainHandler = new DomainHandler(this, domain);
                    handlers.Add(domain, domainHandler);
                }
                handler = domainHandler;
            }

            handler.QueueRevert(device, member, offset);
        }

        private void RemoveDomain(IMachine machine)
        {
            if(handlers.ContainsKey(machine))
            {
                handlers.Remove(machine);
            }
        }

        private ExternalsHandler externalsHandler;
        private readonly SimpleCache cache = new SimpleCache();
        private readonly Monitor.DeviceHandlingHelpers helpers;
        private readonly IDictionary<IMachine, DomainHandler> handlers = new Dictionary<IMachine, DomainHandler>();

        private class DomainHandler : Handler
        {
            public DomainHandler(SetAndRevertAfterCommand parent, IMachine machine) : base(parent)
            {
                this.machine = machine;
                machine.ClockSource.AddClockEntry(new ClockEntry(
                    period: 0,
                    frequency: TimeInterval.TicksPerSecond,
                    // clock entry handler is executed in lock, so there is no need to wrap Update
                    handler: Update,
                    owner: machine,
                    localName: $"{nameof(SetAndRevertAfterCommand)}",
                    enabled: false,
                    workMode: WorkMode.OneShot
                ));
            }

            public override void QueueRevert(object device, MemberInfo member, TimeInterval offset)
            {
                machine.ClockSource.ExecuteInLock(() =>
                {
                    base.QueueRevert(device, member, offset);
                });
            }

            protected override void UpdateSchedule(TimeInterval? interval)
            {
                machine.ClockSource.ExchangeClockEntryWith(Update,
                    entry => entry.With(period: interval?.Ticks ?? 0, enabled: interval != null)
                );
            }

            protected override TimeInterval GetCurrentTime()
            {
                return machine.ClockSource.CurrentValue;
            }

            private readonly IMachine machine;
        }

        private class ExternalsHandler : Handler
        {
            public ExternalsHandler(SetAndRevertAfterCommand parent) : base(parent)
            {
                emulation = EmulationManager.Instance.CurrentEmulation;
                emulation.MasterTimeSource.TimePassed += _ => Update();
            }

            public override void QueueRevert(object device, MemberInfo member, TimeInterval offset)
            {
                lock(locker)
                {
                    base.QueueRevert(device, member, offset);
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

        private abstract class Handler
        {
            public virtual void QueueRevert(object device, MemberInfo member, TimeInterval offset)
            {
                var now = GetCurrentTime();
                var ts = now + offset;

                var revert = new Revert(ts, device, member);

                var node = reverts.First;
                while(node != null && node.Value.Timestamp <= ts)
                {
                    var next = node.Next;
                    if(node.Value.SameTarget(revert))
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

                revert.Value = revert.Value ?? DeviceHelper.InvokeGet(device, member);
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

            protected Handler(SetAndRevertAfterCommand parent)
            {
                this.parent = parent;
            }

            protected virtual void Update()
            {
                var now = GetCurrentTime();
                var node = reverts.First;
                while(node != null && node.Value.Timestamp <= now)
                {
                    this.Trace($"sara: Executing {node.Value}");
                    DeviceHelper.InvokeSet(node.Value.Device, node.Value.Accessor, node.Value.Value);
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

            private readonly SetAndRevertAfterCommand parent;
            // NOTE: Kept in order by Revert.Timestamp
            private readonly LinkedList<Revert> reverts = new LinkedList<Revert>();

            protected struct Revert
            {
                public Revert(TimeInterval timestamp, object device, MemberInfo accessor)
                {
                    Timestamp = timestamp;
                    Device = device;
                    Accessor = accessor;
                    Value = null;
                }

                public bool SameTarget(Revert other)
                {
                    return Device == other.Device && Accessor == other.Accessor;
                }

                public override string ToString() => $"{{<{Device}>.<{Accessor}> := {Value} @ {Timestamp}}}";

                public readonly TimeInterval Timestamp;
                public readonly object Device;
                public readonly MemberInfo Accessor;
                public object Value;
            }
        }
    }
}
