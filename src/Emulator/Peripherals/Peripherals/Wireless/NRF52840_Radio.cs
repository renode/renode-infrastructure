//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Miscellaneous;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Wireless
{
    public class NRF52840_Radio : BasicDoubleWordPeripheral, IRadio, IKnownSize, INRFEventProvider
    {
        public NRF52840_Radio(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();
            interruptManager = new InterruptManager<Events>(this, IRQ, "RadioIrq");
            shorts = new Shorts();
            events = new IFlagRegisterField[(int)Events.PHYEnd + 1];
            rxBuffer = new ConcurrentQueue<KeyValuePair<byte[], IRadio>>();
            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            radioState = State.Disabled;
            interruptManager.Reset();
            base.Reset();
            addressPrefixes = new byte[8];
            while(rxBuffer.TryDequeue(out var _)) { }
        }

        public void FakePacket()
        {
            ReceiveFrame(new byte[]{0xD6, 0xBE, 0x89, 0x8E, 0x60, 0x11, 0xFF, 0xFF, 0xFF, 0xFF, 0x0, 0xC0, 0x2, 0x1, 0x6, 0x7, 0x3, 0xD, 0x18, 0xF, 0x18, 0xA, 0x18, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0}, null);
        }

        public void ReceiveFrame(byte[] frame, IRadio sender)
        {
            if(radioState != State.RxIdle)
            {
                rxBuffer.Enqueue(new KeyValuePair<byte[], IRadio>(frame, sender));
                return;
            }

            var addressLength = (int)baseAddressLength.Value + 1;
            var headerLengthInAir = HeaderLengthInAir();
            var headerLengthInRAM = HeaderLengthInRAM();

            var dataAddress = (uint)packetPointer.Value;
            sysbus.WriteBytes(frame, address: dataAddress, startingIndex: addressLength, count: headerLengthInRAM);
            var payloadLength = Math.Min(frame[addressLength + (int)s0Length.Value], (byte)maxPacketLength.Value);
            sysbus.WriteBytes(frame, address: (ulong)(dataAddress + headerLengthInRAM), startingIndex: addressLength + headerLengthInAir, count: payloadLength);

            var crcLen = 4;
            ScheduleRadioEvents((uint)(headerLengthInAir + payloadLength + crcLen));
        }

        public event Action<IRadio, byte[]> FrameSent;

        public event Action<uint> EventTriggered;

        public int Channel 
        { 
            get
            {
                return bluetoothLEChannelMap.TryGetValue(Frequency, out var result)
                    ? result
                    : -1;
            }
            
            set 
            { 
                throw new RecoverableException("Setting channel manually is not supported");
            } 
        }

        public long Size => 0x1000;

        public uint Frequency => (frequencyMap.Value ? 2360 : 2400U) + (uint)frequency.Value;

        public GPIO IRQ { get; }

        private void DefineTask(Registers register, Action callback, string name)
        {
            register.Define(this, name: name)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, value) => { if(value) callback(); })
                .WithReservedBits(1, 31)
            ;
        }

        private void DefineEvent(Registers register, Action callbackOnSet, Events @event, string name)
        {
            register.Define(this, name: name)
                .WithFlag(0, out events[(int)@event], writeCallback: (_, value) =>
                {
                    if(value)
                    {
                        callbackOnSet();
                    }
                    else
                    {
                        interruptManager.SetInterrupt(@event, false);
                    }
                })
                .WithReservedBits(1, 31)
            ;
        }

        private void DefineRegisters()
        {
            DefineTask(Registers.TxEnable, TxEnable, "TASKS_TXEN");

            DefineTask(Registers.RxEnable, RxEnable, "TASKS_RXEN");

            DefineTask(Registers.Start, Start, "TASKS_START");

            DefineTask(Registers.Disable, Disable, "TASKS_DISABLE");

            DefineEvent(Registers.Ready, () => this.Log(LogLevel.Error, "Trying to trigger READY event, not supported"), Events.Ready, "EVENTS_READY");
            DefineEvent(Registers.AddressSentOrReceived, () => this.Log(LogLevel.Error, "Trying to trigger ADDRESS event, not supported"), Events.Address, "EVENTS_ADDRESS");
            DefineEvent(Registers.PayloadSentOrReceived, () => this.Log(LogLevel.Error, "Trying to trigger PAYLOAD event, not supported"), Events.Payload, "EVENTS_PAYLOAD");
            DefineEvent(Registers.PacketSentOrReceived, () => this.Log(LogLevel.Error, "Trying to trigger END event, not supported"), Events.End, "EVENTS_END");
            DefineEvent(Registers.BitCounterMatch, () => this.Log(LogLevel.Error, "Trying to trigger BCMATCH event, not supported"), Events.BitCountMatch, "EVENTS_BCMATCH");
            DefineEvent(Registers.RSSIEnd, () => this.Log(LogLevel.Error, "Trying to trigger RSSIEnd event, not supported"), Events.RSSIEnd, "EVENTS_RSSIEND");
            DefineEvent(Registers.CRCOk, () => this.Log(LogLevel.Error, "Trying to trigger CRCOk event, not supported"), Events.CRCOk, "EVENTS_CRCOK");

            DefineEvent(Registers.RadioDisabled, Disable, Events.Disabled, "EVENTS_DISABLED");

            DefineEvent(Registers.TxReady, TxEnable, Events.TxReady, "EVENTS_TXREADY");

            DefineEvent(Registers.RxReady, RxEnable, Events.RxReady, "EVENTS_RXREADY");

            // Notice: while the whole Shorts register is implemented, we don't necessarily
            // support all the mentioned events and tasks
            Registers.Shorts.Define(this)
                .WithFlag(0, out shorts.ReadyStart, name: "READY_START")
                .WithFlag(1, out shorts.EndDisable, name: "END_DISABLE")
                .WithFlag(2, out shorts.DisabledTxEnable, name: "DISABLED_TXEN")
                .WithFlag(3, out shorts.DisabledRxEnable, name: "DISABLED_RXEN")
                .WithFlag(4, out shorts.AddressRSSIStart, name: "ADDRESS_RSSISTART")
                .WithFlag(5, out shorts.EndStart, name: "END_START")
                .WithFlag(6, out shorts.AddressBitCountStart, name: "ADDRESS_BCSTART")
                .WithFlag(8, out shorts.DisabledRSSIStop, name: "DISABLED_RSSISTOP")
                .WithFlag(11, out shorts.RxReadyCCAStart, name: "RXREADY_CCASTART")
                .WithFlag(12, out shorts.CCAIdleTxEnable, name: "CCAIDLE_TXEN")
                .WithFlag(13, out shorts.CCABusyDisable, name: "CCABUSY_DISABLE")
                .WithFlag(14, out shorts.FrameStartBitCountStart, name: "FRAMESTART_BCSTART")
                .WithFlag(15, out shorts.ReadyEnergyDetectStart, name: "READY_EDSTART")
                .WithFlag(16, out shorts.EnergyDetectEndDisable, name: "EDEND_DISABLE")
                .WithFlag(17, out shorts.CCAIdleStop, name: "CCAIDLE_STOP")
                .WithFlag(18, out shorts.TxReadyStart, name: "TXREADY_START")
                .WithFlag(19, out shorts.RxReadyStart, name: "RXREADY_START")
                .WithFlag(20, out shorts.PHYEndDisable, name: "PHYEND_DISABLE")
                .WithFlag(21, out shorts.PHYEndStart, name: "PHYEND_START")
                .WithReservedBits(22, 10)
            ;

            RegistersCollection.AddRegister((long)Registers.InterruptEnable,
                interruptManager.GetInterruptEnableSetRegister<DoubleWordRegister>());

            RegistersCollection.AddRegister((long)Registers.InterruptDisable,
                interruptManager.GetInterruptEnableClearRegister<DoubleWordRegister>());

            Registers.CRCStatus.Define(this)
               .WithFlag(0, name: "CRCSTATUS", valueProviderCallback: _ => true) // we assume here that CRCs are always ok
               .WithReservedBits(1, 31)
            ;

            Registers.PacketPointer.Define(this)
                .WithValueField(0, 32, out packetPointer, name: "PACKETPTR")
            ;

            Registers.Frequency.Define(this, name: "FREQUENCY")
                .WithValueField(0, 7, out frequency, name: "FREQUENCY")
                .WithReservedBits(7, 1)
                .WithFlag(8, out frequencyMap, name: "MAP")
                .WithReservedBits(9, 23)
            ;

            Registers.TxPower.Define(this, name: "TXPOWER")
                .WithValueField(0, 8, name: "TXPOWER") // just RW
                .WithReservedBits(8, 24)
            ;

            Registers.Mode.Define(this, name: "MODE")
                .WithValueField(0, 4, name: "MODE") // just RW
                .WithReservedBits(4, 24)
            ;

            Registers.PacketConfiguration0.Define(this, name: "PCNF0")
                .WithValueField(0, 4, out lengthFieldLength, name: "LFLEN")
                .WithReservedBits(4, 4)
                .WithValueField(8, 1, out s0Length, name: "S0LEN")
                .WithReservedBits(9, 7)
                .WithValueField(16, 4, out s1Length, name: "S1LEN")
                .WithFlag(20, out s1Include, name: "S1INCL")
                .WithReservedBits(21, 1)
                .WithValueField(22, 2, out codeIndicatorLength, name: "CILEN")
                .WithTag("PLEN", 24, 2)
                .WithFlag(26, out crcIncludedInLength, name: "CRCINC")
                .WithReservedBits(27, 2)
                .WithValueField(29, 2, out termLength, name: "TERMLEN")
                .WithReservedBits(31, 1)
            ;

            Registers.PacketConfiguration1.Define(this, name: "PCNF1")
                .WithValueField(0, 8, out maxPacketLength, name: "MAXLEN")
                .WithValueField(8, 8, out staticLength, name: "STATLEN")
                .WithValueField(16, 3, out baseAddressLength, name: "BALEN")
                .WithReservedBits(19, 5)
                .WithTaggedFlag("ENDIAN", 24)
                .WithTaggedFlag("WHITEEN", 25)
                .WithReservedBits(26, 6)
            ;

            Registers.BaseAddress0.Define(this)
                .WithValueField(0, 32, out baseAddress0, name: "BASE0")
            ;

            Registers.BaseAddress1.Define(this)
                .WithValueField(0, 32, out baseAddress1, name: "BASE1")
            ;

            Registers.Prefix0.Define(this, name: "PREFIX0")
                .WithValueFields(0, 8, 4, writeCallback: (i, _, value) => addressPrefixes[i] = (byte)value, name: "AP")
            ;

            Registers.Prefix1.Define(this, name: "PREFIX1")
                .WithValueFields(0, 8, 4, writeCallback: (i, _, value) => addressPrefixes[4 + i] = (byte)value, name: "AP")
            ;

            Registers.TxAddress.Define(this, name: "TXADDRESS")
                .WithValueField(0, 3, out txAddress, name: "TXADDRESS")
                .WithReservedBits(3, 29)
            ;

            Registers.RxAddresses.Define(this, name: "RXADDRESSES")
                .WithFlags(0, 8, out rxAddressEnabled, name: "ADDR")
            ;

            Registers.CRCConfiguration.Define(this, name: "CRCCNF")
                .WithValueField(0, 2, out crcLength, name: "LEN")
                .WithReservedBits(2, 6)
                .WithEnumField(8, 2, out crcSkipAddress, name: "SKIPADDR")
                .WithReservedBits(10, 21)
            ;

            Registers.CRCPolynomial.Define(this, name: "CRCPOLY")
                .WithValueField(0, 24, out crcPolynomial, name: "CRCPOLY")
                .WithReservedBits(24, 8)
            ;

            Registers.CRCInitialValue.Define(this, name: "CRCINIT")
                .WithValueField(0, 24, out crcInitialValue, name: "CRCINIT")
                .WithReservedBits(24, 8)
            ;

            Registers.RSSISample.Define(this, name: "RSSISAMPLE")
                .WithValueField(0, 32, valueProviderCallback: _ => DefaultRSSISample, name: "RSSISAMPLE");
            ;

            Registers.State.Define(this, name: "STATE")
                .WithEnumField<DoubleWordRegister, State>(0, 4, FieldMode.Read, valueProviderCallback: _ => radioState, name: "STATE")
                .WithReservedBits(4, 28)
            ;

            Registers.BitCounterCompare.Define(this, name: "BCC")
                .WithValueField(0, 32, out bitCountCompare)
            ;

            Registers.ModeConfiguration0.Define(this, 0x200)
                .WithTaggedFlag("RU", 0)
                .WithReservedBits(1, 7)
                .WithTag("DTX", 8, 2)
                .WithReservedBits(10, 22)
            ;

            Registers.CCAControl.Define(this, 0x052D0000, name: "CCACTRL")
                .WithEnumField(0, 3, out ccaMode, name: "CCAMODE")
                .WithReservedBits(3, 5)
                .WithTag("CCAEDTHRES", 8, 8)
                .WithTag("CCACORRTHRES", 16, 8)
                .WithTag("CCACORRCNT", 24, 8)
            ;

            Registers.PowerControl.Define(this, 1, name: "POWER")
                //TODO: radio should be disabled with powerOn == false
                .WithFlag(0, out powerOn, changeCallback: (_, value) => { if(!value) Reset(); }, name: "POWER")
                .WithReservedBits(1, 31)
            ;
        }

        private void LogUnhandledShort(IFlagRegisterField field, string shortName)
        {
            if(field.Value)
            {
                this.Log(LogLevel.Error, $"Unhandled short {shortName}!");
            }
        }

        private void SetEvent(Events @event)
        {
            interruptManager.SetInterrupt(@event);
            events[(int)@event].Value = true;
            EventTriggered?.Invoke((uint)@event * 4 + 0x100);
        }

        private void Disable()
        {
            radioState = State.Disabled;
            SetEvent(Events.Disabled);
            LogUnhandledShort(shorts.DisabledRSSIStop, nameof(shorts.DisabledRSSIStop));
            LogUnhandledShort(shorts.DisabledRxEnable, nameof(shorts.DisabledRxEnable));
            LogUnhandledShort(shorts.DisabledTxEnable, nameof(shorts.DisabledTxEnable));
        }

        // These comments sum up some details gathered from the documentation.
        //
        // Packet:
        // preamble, address (base + prefix), CI, TERM1, S0, LENGTH, S1, PAYLOAD, STATIC PAYLOAD (from STATLEN), CRC, TERM2

        // Packet data stored in RAM:
        // S0, Length, S1, Payload

        // CRC:
        // start with address, start with CI or start with L1

        // S0 + Length + S1 + payload <= 258 bytes

        // transmit sequence
        // TXEn -> State.TxRampup -> Event.Ready -> State.TxIdle.  Start -> State.Tx. Sending data: P, A, --> Event.Address,  S0, L, S1, Payload, --> Event.Payload, CRC, --> Event.End (data finished) -> State.TxIdle.
        // Disable -> State.TxDisable -> Event.Disabled

        // transmit sequence with shortcuts
        // TXEn -> State.TxRampup -> Event.Ready shortcut to Start, Event.End shortcut to Disable

        // transmit multiple packets
        // no shortcut end->disable. After Event.End, send start

        // receive sequence
        // RXEn -> State.RxRampup -> Event.Ready -> State.RxIdle. Start->State.Rx. Receiving data: P, A --> Event.Address, s0, l, s1, payload  --> Event.Payload, crc -->Event.End (data finished) --> State.RxIdle
        // Disable --> State.RxDisable -> Event.Disabled

        private void TxEnable()
        {
            radioState = State.TxIdle;
            // we're skipping rampup, it's instant

            SetEvent(Events.Ready);
            SetEvent(Events.TxReady);

            if(shorts.ReadyStart.Value || shorts.TxReadyStart.Value)
            {
                Start();
            }
            LogUnhandledShort(shorts.ReadyEnergyDetectStart, nameof(shorts.ReadyEnergyDetectStart));
        }

        private void RxEnable()
        {
            radioState = State.RxIdle;
            // we're skipping rampup, it's instant

            SetEvent(Events.Ready);
            SetEvent(Events.RxReady);

            if(shorts.ReadyStart.Value || shorts.RxReadyStart.Value)
            {
                Start();
            }
            LogUnhandledShort(shorts.ReadyEnergyDetectStart, nameof(shorts.ReadyEnergyDetectStart));
        }

        private void Start()
        {
            // common task for both reception and transmission
            if(radioState == State.TxIdle)
            {
                SendPacket();
            }
            else if(radioState == State.RxIdle)
            {
                // Can only receive one packet per enable
                if(rxBuffer.TryDequeue(out var pair))
                {
                    ReceiveFrame(pair.Key, pair.Value);
                }
            }
            else
            {
                this.Log(LogLevel.Error, "Triggered the Start task in an unexpected state {0}", radioState.ToString());
            }
        }

        private int HeaderLengthInAir()
        {
           return (int)Math.Ceiling((s0Length.Value * 8 + lengthFieldLength.Value + s1Length.Value) / 8.0);
        }

        private int HeaderLengthInRAM()
        {
           int ret = (int)s0Length.Value + (int)Math.Ceiling(lengthFieldLength.Value / 8.0) + (int)Math.Ceiling(s1Length.Value / 8.0);
           if(s1Length.Value == 0 && s1Include.Value)
           {
              ret += 1;
           }
           return ret;
        }

        private void SendPacket()
        {
            var dataAddress = (uint)packetPointer.Value;
            var headerLengthInAir = HeaderLengthInAir();
            var addressLength = (int)baseAddressLength.Value + 1;
            var headerLengthInRAM = HeaderLengthInRAM();
            if(headerLengthInAir != headerLengthInRAM)
            {
                this.Log(LogLevel.Noisy, "Header length difference between onAir={0} and inRam={1}", headerLengthInAir, headerLengthInRAM);
            }

            var data = new byte[addressLength + headerLengthInRAM + (uint)maxPacketLength.Value];
            FillCurrentAddress(data, 0, (uint)txAddress.Value);

            sysbus.ReadBytes(dataAddress, headerLengthInRAM, data, addressLength);
            this.Log(LogLevel.Noisy, "Header: {0} S0 {1} Length {2} S1 {3} s1inc {4}", Misc.PrettyPrintCollectionHex(data), s0Length.Value, lengthFieldLength.Value, s1Length.Value, s1Include.Value);
            var payloadLength = data[addressLength + (uint)s0Length.Value];
            if(payloadLength > maxPacketLength.Value)
            {
                this.Log(LogLevel.Error, "Payload length ({0}) longer than the max packet length ({1}), trimming...", payloadLength, maxPacketLength.Value);
                payloadLength = (byte)maxPacketLength.Value;
            }
            sysbus.ReadBytes((ulong)(dataAddress + headerLengthInRAM), payloadLength, data, addressLength + headerLengthInAir);
            this.Log(LogLevel.Noisy, "Data: {0} Maxlen {1} statlen {2}", Misc.PrettyPrintCollectionHex(data), maxPacketLength.Value, staticLength.Value);

            FrameSent?.Invoke(this, data);

            var crcLen = 4;
            ScheduleRadioEvents((uint)(headerLengthInAir + payloadLength + crcLen));

            LogUnhandledShort(shorts.EndStart, nameof(shorts.EndStart)); // not sure how to support it. It's instant from our perspective.
        }

        private void ScheduleRadioEvents(uint packetLen)
        {
           var timeSource = machine.LocalTimeSource;
           var now = timeSource.ElapsedVirtualTime;

           // @note  Transmit times assume 1M PHY. Low level BLE firmware
           //        usually takes into account the active phy when calculating
           //        timing delays, so we might need to do that.

           // Bit-counter
           var bcMatchTime = now + TimeInterval.FromMicroseconds(bitCountCompare.Value);
           var bcMatchTimeStamp = new TimeStamp(bcMatchTime, timeSource.Domain);

           // End event
           var endTime = now + TimeInterval.FromMicroseconds((uint)(packetLen) * 8);
           var endTimeStamp = new TimeStamp(endTime, timeSource.Domain);

           var disableTime = endTime + TimeInterval.FromMicroseconds(10);
           var disableTimeStamp = new TimeStamp(disableTime, timeSource.Domain);

           // Address modelled as happening immediatley and serves as anchor
           // point for other events. RIOT triggers IRQ from it.
           SetEvent(Events.Address);

           // RSSI sample period is 0.25 us. Acceptable to model as happening
           // immediatley upon start
           if(shorts.AddressRSSIStart.Value)
           {
              SetEvent(Events.RSSIEnd);
           }

           // Schedule a single bit-counter compare event not eariler than
           // `bitCountCompare` microseconds from now.
           // This is sufficient for BLE with RIOT stack, however it is possible to use bit
           // counter to generate successive events, which this model will not
           // support.
           if(shorts.AddressBitCountStart.Value)
           {
              timeSource.ExecuteInSyncedState(_ =>
              {
                 SetEvent(Events.BitCountMatch);
              }, bcMatchTimeStamp);
           }

           // Schedule "end" events all at once, simulating the transmision time
           // as 8 microseconds-per-byte. Timing distinction between events here doesn't
           // seem important
           timeSource.ExecuteInSyncedState(_ =>
           {
              SetEvent(Events.Payload);
              SetEvent(Events.End);
              SetEvent(Events.CRCOk);
           }, endTimeStamp);

           // BLE stacks use disabled event as common processing trigger.
           timeSource.ExecuteInSyncedState(_ =>
           {
              if(shorts.EndDisable.Value)
              {
                 Disable();
              }
           }, disableTimeStamp);
        }

        private void FillCurrentAddress(byte[] data, int startIndex, uint logicalAddress)
        {
            // based on 6.20.2 Address configuration
            var baseAddress = (uint)(logicalAddress == 0 ? baseAddress0.Value : baseAddress1.Value);
            var baseBytes = BitConverter.GetBytes(baseAddress);
            var i = 0;
            if(baseAddressLength.Value > 4)
            {
                this.Log(LogLevel.Error, "Trying to fill the current address, but the base address length is too large ({0}). Limiting to 4.", baseAddressLength.Value);
                baseAddressLength.Value = 4;
            }
            for(var j = 4 - baseAddressLength.Value; j < 4; i++, j++) // we're not supporting BALEN > 4. I don't know how  should it work.
            {
                data[startIndex + i] = baseBytes[j];
            }
            data[startIndex + i] = addressPrefixes[logicalAddress];
        }
        
        private const int DefaultRSSISample = 10;

        private readonly ConcurrentQueue<KeyValuePair<byte[], IRadio>> rxBuffer;
        private Shorts shorts;
        private byte[] addressPrefixes;
        private State radioState;
        private InterruptManager<Events> interruptManager;

        private IFlagRegisterField[] events;
        private IValueRegisterField packetPointer;
        private IValueRegisterField frequency;
        private IFlagRegisterField frequencyMap;
        private IValueRegisterField lengthFieldLength;
        private IValueRegisterField s0Length;
        private IValueRegisterField s1Length;
        private IFlagRegisterField s1Include;
        private IValueRegisterField codeIndicatorLength;
        private IFlagRegisterField crcIncludedInLength;
        private IValueRegisterField termLength;
        private IValueRegisterField maxPacketLength;
        private IValueRegisterField staticLength;
        private IValueRegisterField baseAddressLength;
        private IValueRegisterField baseAddress0;
        private IValueRegisterField baseAddress1;
        private IValueRegisterField txAddress;
        private IFlagRegisterField[] rxAddressEnabled;

        private IValueRegisterField bitCountCompare;

        private IValueRegisterField crcLength;
        private IEnumRegisterField<CRCAddressHandling> crcSkipAddress;
        private IValueRegisterField crcPolynomial;
        private IValueRegisterField crcInitialValue;
        private IEnumRegisterField<CCAMode> ccaMode;
        private IFlagRegisterField powerOn;
        
        private readonly Dictionary<uint, int> bluetoothLEChannelMap = new Dictionary<uint, int>()
        {
           { 2402, 37 },
           { 2404, 0 },
           { 2406, 1 },
           { 2408, 2 },
           { 2410, 3 },
           { 2412, 4 },
           { 2414, 5 },
           { 2416, 6 },
           { 2418, 7 },
           { 2420, 8 },
           { 2422, 9 },
           { 2424, 10 },
           { 2426, 38 },
           { 2428, 11 },
           { 2430, 12 },
           { 2432, 13 },
           { 2434, 14 },
           { 2436, 15 },
           { 2438, 16 },
           { 2440, 17 },
           { 2442, 18 },
           { 2444, 19 },
           { 2446, 20 },
           { 2448, 21 },
           { 2450, 22 },
           { 2452, 23 },
           { 2454, 24 },
           { 2456, 25 },
           { 2458, 26 },
           { 2460, 27 },
           { 2462, 28 },
           { 2464, 29 },
           { 2466, 30 },
           { 2468, 31 },
           { 2470, 32 },
           { 2472, 33 },
           { 2474, 34 },
           { 2476, 35 },
           { 2478, 36 },
           { 2480, 39 },
        };
        
        private struct Shorts
        {
            public IFlagRegisterField ReadyStart;
            public IFlagRegisterField EndDisable;
            public IFlagRegisterField DisabledTxEnable;
            public IFlagRegisterField DisabledRxEnable;
            public IFlagRegisterField AddressRSSIStart;
            public IFlagRegisterField EndStart;
            public IFlagRegisterField AddressBitCountStart;
            public IFlagRegisterField DisabledRSSIStop;
            public IFlagRegisterField RxReadyCCAStart;
            public IFlagRegisterField CCAIdleTxEnable;
            public IFlagRegisterField CCABusyDisable;
            public IFlagRegisterField FrameStartBitCountStart;
            public IFlagRegisterField ReadyEnergyDetectStart;
            public IFlagRegisterField EnergyDetectEndDisable;
            public IFlagRegisterField CCAIdleStop;
            public IFlagRegisterField TxReadyStart;
            public IFlagRegisterField RxReadyStart;
            public IFlagRegisterField PHYEndDisable;
            public IFlagRegisterField PHYEndStart;
        }

        private enum CRCAddressHandling
        {
            Include = 0,
            Skip = 1,
            IEEE802154 = 2
        }

        private enum State
        {
            Disabled = 0,
            RxRampup = 1,
            RxIdle = 2,
            Rx = 3,
            RxDisable = 4,
            TxRampup = 9,
            TxIdle = 10,
            Tx = 11,
            TxDisable = 12,
        }
        
        private enum Events
        {
            Ready = 0,
            Address = 1,
            Payload = 2,
            End = 3,
            Disabled = 4,
            DeviceAddressMatch = 5,
            DeviceAddressMiss = 6,
            RSSIEnd = 7,
            BitCountMatch = 10,
            CRCOk = 12,
            CRCError = 13,
            FrameStart = 14,
            EnergyDetectEnd = 15,
            EnergyDetectStopped = 16,
            CCAIdle = 17,
            CCABusy = 18,
            CCAStopped = 19,
            RateBoost = 20,
            TxReady = 21,
            RxReady = 22,
            MACHeaderMatch = 23,
            Sync = 26,
            PHYEnd = 27
        }
        
        private enum CCAMode
        {
            EdMode,
            CarrierMode,
            CarrierAndEdMode,
            CarrierOrEdMode,
            EdMoteTest1
        }
        
        private enum Registers
        {
            TxEnable = 0x000,
            RxEnable = 0x004,
            Start = 0x008,
            Stop = 0x00C,
            Disable = 0x010,
            RSSIStart = 0x014,
            RSSIStop = 0x018,
            BitCounterStart = 0x01C,
            BitCounterStop = 0x020,
            EnergyDetectStart = 0x024,
            EnergyDetectStop = 0x028,
            CCAStart = 0x02C,
            CCAStop = 0x030,
            Ready = 0x100,
            AddressSentOrReceived = 0x104,
            PayloadSentOrReceived = 0x108,
            PacketSentOrReceived = 0x10C,
            RadioDisabled = 0x110,
            DeviceMatch = 0x114,
            DeviceMiss = 0x118,
            RSSIEnd = 0x11C,
            BitCounterMatch = 0x128,
            CRCOk = 0x130,
            CRCError = 0x134,
            FrameStartReceived = 0x138,
            EnergyDetectEnd = 0x13C,
            EnergyDetectStopped = 0x140,
            CCAIdle = 0x144,
            CCABusy = 0x148,
            CCAStopped = 0x14C,
            RateBoost = 0x150,
            TxReady = 0x154,
            RxReady = 0x158,
            MACHeaderMatch = 0x15C,
            Sync = 0x168,
            PHYEnd = 0x16C,
            Shorts = 0x200,
            InterruptEnable = 0x304,
            InterruptDisable = 0x308,
            CRCStatus = 0x400,
            RxMatch = 0x408,
            RxCRC = 0x40C,
            DeviceAddressMatchIndex = 0x410,
            PayloadStatus = 0x414,
            PacketPointer = 0x504,
            Frequency = 0x508,
            TxPower = 0x50C,
            Mode = 0x510,
            PacketConfiguration0 = 0x514,
            PacketConfiguration1 = 0x518,
            BaseAddress0 = 0x51C,
            BaseAddress1 = 0x520,
            Prefix0 = 0x524,
            Prefix1 = 0x528,
            TxAddress = 0x52C,
            RxAddresses = 0x530,
            CRCConfiguration = 0x534,
            CRCPolynomial = 0x538,
            CRCInitialValue = 0x53C,
            InterframeSpacing = 0x544,
            RSSISample = 0x548,
            State = 0x550,
            DataWhiteningInitialValue = 0x554,
            BitCounterCompare = 0x560,
            DeviceAddressBaseSegment0 = 0x600,
            DeviceAddressBaseSegment1 = 0x604,
            DeviceAddressBaseSegment2 = 0x608,
            DeviceAddressBaseSegment3 = 0x60C,
            DeviceAddressBaseSegment4 = 0x610,
            DeviceAddressBaseSegment5 = 0x614,
            DeviceAddressBaseSegment6 = 0x618,
            DeviceAddressBaseSegment7 = 0x61C,
            DeviceAddressPrefix0 = 0x620,
            DeviceAddressPrefix1 = 0x624,
            DeviceAddressPrefix2 = 0x628,
            DeviceAddressPrefix3 = 0x62C,
            DeviceAddressPrefix4 = 0x630,
            DeviceAddressPrefix5 = 0x634,
            DeviceAddressPrefix6 = 0x638,
            DeviceAddressPrefix7 = 0x63C,
            DeviceAddressMatchConfiguration = 0x640,
            SearchPatternConfiguration = 0x644,
            PatternMask = 0x648,
            ModeConfiguration0 = 0x650,
            StartOfFrameDelimiter = 0x660,
            EnergyDetectLoopCount = 0x664,
            EnergyDetectLevel = 0x668,
            CCAControl = 0x66C,
            PowerControl = 0xFFC
        }
    }
}
