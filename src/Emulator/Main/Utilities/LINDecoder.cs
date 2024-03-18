//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Linq;
using System.Collections.Generic;

namespace Antmicro.Renode.Utilities
{
    public interface ILINEntry
    {
        LINMode Mode { get; set; }
        int FrameLength { get; set; }
        bool ValidateFrame { get; set; }
        bool ExtendedChecksum { get; set; }

        event Action<byte[], bool> DataReady;
        event Action StartedTransmission;
    }

    public enum LINMode
    {
        Source,
        Sink,
    }

    public class LINDecoder
    {
        public LINDecoder()
        {
            entries = new SortedList<byte, LINEntry>();
            rxQueue = new Queue<byte>();
            txQueue = new Queue<byte>();
        }

        public ILINEntry Register(byte protectedIdentifier)
        {
            if(ValidateProtectedIdentifier && !IsProtectedIdentifierValid(protectedIdentifier))
            {
                throw new ArgumentException("invalid protected identifier", "protectedIdentifier");
            }

            entries.Add(protectedIdentifier, new LINEntry(protectedIdentifier));
            return entries[protectedIdentifier];
        }

        public void Unregister(byte protectedIdentifier)
        {
            entries.Remove(protectedIdentifier);
        }

        public bool TryGetEntry(byte protectedIdentifier, out ILINEntry entry)
        {
            if(entries.TryGetValue(protectedIdentifier, out var linEntry))
            {
                entry = (ILINEntry)linEntry;
                return true;
            }

            entry = null;
            return false;
        }

        public ILINEntry GetEntry(byte protectedIdentifier)
        {
            if(TryGetEntry(protectedIdentifier, out var entry))
            {
                return entry;
            }
            throw new ArgumentNullException($"no entry with pid 0x{protectedIdentifier:X02}");
        }

        public void Feed(byte value)
        {
            var previousState = CurrentState;
            switch(CurrentState)
            {
                case State.Armed:
                {
                    // We are waiting for synchronization
                    if(value != SynchronizationPattern)
                    {
                        CurrentState = State.SynchronizationFailed;
                        break;
                    }
                    CurrentState = State.Synchronized;
                    break;
                }

                case State.Synchronized:
                {
                    // We are awaiting Protected Identifier
                    if(!entries.TryGetValue(value, out var entry))
                    {
                        // This transaction is not for us
                        CurrentState = State.Ignoring;
                        break;
                    }
                    currentProtectedIdentifier = value;
                    CurrentState = entry.Mode == LINMode.Source ? State.Writing : State.Reading;
                    break;
                }

                case State.SynchronizationFailed:
                    // NOTE: Intentionally left empty
                    break;

                case State.Writing:
                    // NOTE: Intentionally left empty
                    break;

                case State.Reading:
                {
                    rxQueue.Enqueue(value);
                    var entry = CurrentEntry;
                    if(rxQueue.Count != entry.FrameLength + 1)
                    {
                        return;
                    }

                    var frame = rxQueue.Take(entry.FrameLength).ToArray();
                    var crc = rxQueue.Skip(entry.FrameLength).First();
                    var frameValid = !entry.ValidateFrame || entry.IsFrameValid(frame, crc);

                    FrameReceived?.Invoke(currentProtectedIdentifier, frame, frameValid);
                    entry.InvokeDataReady(frame, frameValid);

                    CurrentState = State.Ignoring;
                    break;
                }

                case State.Ignoring:
                    // NOTE: Intentionally left empty
                    return;

                default:
                    throw new Exception("unreachable");
            }

            if(CurrentState == State.Writing)
            {
                CurrentEntry.InvokeStartedTransmission();
            }
        }

        public void Transmit(byte[] data)
        {
            txQueue.EnqueueRange(data);
        }

        public void Transmit(byte b)
        {
            txQueue.Enqueue(b);
        }

        public IEnumerable<byte> Finalize()
        {
            if(CurrentState != State.Ignoring)
            {
                CurrentState = State.Ignoring;
            }

            var dataToTransmit = txQueue.ToList();
            txQueue.Clear();

            var checksum = CurrentEntry.CalculateChecksum(dataToTransmit);
            dataToTransmit.Add(checksum);
            return dataToTransmit;
        }

        public void Finalize(Action<byte> writer)
        {
            var finalizedData = Finalize();
            foreach(var b in finalizedData)
            {
                writer(b);
            }
        }

        public void TransmitAndFinalize(byte[] data, Action<byte> writer)
        {
            Transmit(data);
            Finalize(writer);
        }

        public IEnumerable<byte> TransmitAndFinalize(byte[] data)
        {
            Transmit(data);
            return Finalize();
        }

        public void Break()
        {
            CurrentState = State.Armed;
        }

        public event Action<State, State> StateChanged;
        public event Action<byte, byte[], bool> FrameReceived;

        public State CurrentState
        {
            get => currentState;
            protected set
            {
                if(currentState == value)
                {
                    return;
                }
                var previousState = currentState;
                currentState = value;
                StateChanged?.Invoke(previousState, CurrentState);
            }
        }

        public bool ValidateProtectedIdentifier { get; set; }

        protected bool IsProtectedIdentifierValid(byte pid)
        {
            var frameIdentifier = (byte)(pid & 0x7F);
            var checksum = pid >> 6;
            var frameIdentifierBits = BitHelper.GetBits(frameIdentifier).Select(b => b ? 1 : 0).ToArray();
            var computedChecksum =
                ((1 ^ frameIdentifierBits[1] ^ frameIdentifierBits[3] ^ frameIdentifierBits[4] ^ frameIdentifierBits[5]) << 1) |
                ((frameIdentifierBits[0] ^ frameIdentifierBits[1] ^ frameIdentifierBits[2] ^ frameIdentifierBits[4]) << 0);

            return computedChecksum == checksum;
        }

        protected LINEntry CurrentEntry => entries.TryGetValue(currentProtectedIdentifier, out var entry) ? entry : null;

        protected readonly IDictionary<byte, LINEntry> entries;
        protected readonly Queue<byte> rxQueue;
        protected readonly Queue<byte> txQueue;

        protected const byte SynchronizationPattern = 0x55;

        protected byte currentProtectedIdentifier;
        protected State currentState;

        public enum State
        {
            Ignoring,
            Armed,
            Synchronized,
            SynchronizationFailed,
            Writing,
            Reading,
        }

        protected class LINEntry : ILINEntry
        {
            public LINEntry(byte protectedIdentifier)
            {
                ProtectedIdentifier = protectedIdentifier;
            }

            public byte CalculateChecksum(IEnumerable<byte> data)
            {
                var checksum = ExtendedChecksum ? ProtectedIdentifier : 0;
                foreach(var b in data)
                {
                    checksum += b;
                    if(checksum > 0xFF)
                    {
                        checksum -= 0xFF;
                    }
                }
                return (byte)(checksum ^ 0xFF);
            }

            public bool IsFrameValid(IEnumerable<byte> data, byte checksum)
            {
                return checksum == CalculateChecksum(data);
            }

            public void InvokeDataReady(byte[] frame, bool checksumValid) => DataReady?.Invoke(frame, checksumValid);
            public void InvokeStartedTransmission() => StartedTransmission?.Invoke();

            public byte ProtectedIdentifier { get; }
            public LINMode Mode { get; set; }
            public int FrameLength { get; set; }
            public bool ValidateFrame { get; set; }
            public bool ExtendedChecksum { get; set; }

            public event Action<byte[], bool> DataReady;
            public event Action StartedTransmission;
        }
    }
}
