//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.UART
{
    public static class UARTHubExtensions
    {
        public static void CreateUARTHub(this Emulation emulation, string name, bool loopback = false)
        {
            emulation.ExternalsManager.AddExternal(new UARTHub<byte>(loopback), name);
        }

        public static void CreateWordUARTHub(this Emulation emulation, string name, bool loopback = false)
        {
            emulation.ExternalsManager.AddExternal(new UARTHub<ushort>(loopback), name);
        }

        public static void CreateDoubleWordUARTHub(this Emulation emulation, string name, bool loopback = false)
        {
            emulation.ExternalsManager.AddExternal(new UARTHub<uint>(loopback), name);
        }
    }

    public sealed class UARTHub<T> : UARTHubBase<IUART<T>, T>
        where T : IBinaryInteger<T>
    {
        public UARTHub(bool loopback) : base(loopback) { }
    }

    public class UARTHubBase<I, T> : IExternal, IHasOwnLife, IConnectable<I>
        where I : class, IUART<T>
        where T : IBinaryInteger<T>
    {
        public UARTHubBase(bool loopback)
        {
            uarts = new Dictionary<I, Action<T>>();
            hasWarnedForConnection = new HashSet<Tuple<I, I>>();
            locker = new object();
            shouldLoopback = loopback;
            StrictMode = false;
            maximumFlippedBits = int.MaxValue; // No limit for how many bits can be flipped by default
        }

        public virtual void AttachTo(I uart)
        {
            lock(locker)
            {
                if(uarts.ContainsKey(uart))
                {
                    throw new RecoverableException("Cannot attach to the provided UART as it is already registered in this hub.");
                }

                Action<T> d = null;
                if(uart is IDelayableUART duart)
                {
                    d = x =>
                    {
                        var now = TimeDomainsManager.Instance.VirtualTimeStamp;
                        var when = new TimeStamp(now.TimeElapsed + duart.CharacterTransmissionDelay, now.Domain);
                        HandleCharReceived(x, when, uart);
                    };
                }
                else
                {
                    d = x => HandleCharReceived(x, TimeDomainsManager.Instance.VirtualTimeStamp, uart);
                }
                uarts.Add(uart, d);
                uart.CharReceived += d;
            }
        }

        public void Start()
        {
            Resume();
        }

        public void Pause()
        {
            started = false;
        }

        public void Resume()
        {
            started = true;
        }

        public virtual void DetachFrom(I uart)
        {
            lock(locker)
            {
                if(!uarts.ContainsKey(uart))
                {
                    throw new RecoverableException("Cannot detach from the provided UART as it is not registered in this hub.");
                }

                uart.CharReceived -= uarts[uart];
                uarts.Remove(uart);
            }
        }

        public bool IsPaused => !started;

        public bool StrictMode
        {
            get => strictMode;
            set
            {
                if(value != strictMode)
                {
                    if(value)
                    {
                        this.InfoLog("Enabling strict mode, UART messages with missmatched settings (baud, parity, stop bits) will not be delivered");
                    }
                    strictMode = value;
                }
            }
        }

        public double BitFlipRate
        {
            get => bitFlipRate;
            set => bitFlipRate = ValidateRate(nameof(BitFlipRate), value);
        }

        public int MaximumFlippedBits
        {
            get => maximumFlippedBits;
            set => maximumFlippedBits = value;
        }

        public double DroppedCharacterRate
        {
            get => droppedCharacterRate;
            set => droppedCharacterRate = ValidateRate(nameof(DroppedCharacterRate), value);
        }

        public double FrameErrorRate
        {
            get => frameErrorRate;
            set => frameErrorRate = ValidateRate(nameof(FrameErrorRate), value);
        }

        public event Action<I, T> DataTransmitted;

        public event Action<I, I, T> DataRouted;

        private static readonly Bits[] allStopBitValues = Enum.GetValues<Bits>();
        private static readonly byte[] allBitPositions = Enumerable.Range(0, default(T).GetByteCount() * 8).Select(x => (byte)x).ToArray();

        protected bool started;
        protected bool strictMode;
        protected readonly bool shouldLoopback;
        protected readonly Dictionary<I, Action<T>> uarts;
        protected readonly HashSet<Tuple<I, I>> hasWarnedForConnection;
        protected readonly object locker;

        private void HandleCharReceived(T obj, TimeStamp when, I sender)
        {
            if(!started)
            {
                return;
            }

            DataTransmitted?.Invoke(sender, obj);

            lock(locker)
            {
                var rng = EmulationManager.Instance.CurrentEmulation.RandomGenerator;
                if(rng.NextDouble() < DroppedCharacterRate)
                {
                    this.DebugLog("Dropping character {0} sent by {1}", obj, sender.GetName());
                    return;
                }

                var originalObj = obj;
                obj = FlipBits(rng, obj, out var flipParity);

                foreach(var recipient in uarts.Where(x => shouldLoopback || x.Key != sender).Select(x => x.Key))
                {
                    var compatible = CheckUARTCompatibility(sender, recipient);
                    // Only send extra info in strict mode, as the model might otherwise not deliver the message
                    if(recipient is IUARTWithFrameInfo<T> frameRecipient && StrictMode)
                    {
                        // Frame is created based on the value before any bit flips so that
                        // UART models can spot a mismatch between the actual parity and what is stored in UARTFrame
                        var frame = UARTFrame.CreateFromSenderAndMessage(sender, originalObj);
                        if(flipParity && rng.NextDouble() < BitFlipRate)
                        {
                            frame.ParityBit = frame.ParityBit switch
                            {
                                UARTFrame.ParityBitValue.Zero => UARTFrame.ParityBitValue.One,
                                UARTFrame.ParityBitValue.One => UARTFrame.ParityBitValue.Zero,
                                _ => frame.ParityBit,
                            };
                        }

                        if(rng.NextDouble() < FrameErrorRate)
                        {
                            // Pick any StopBits value different than the original to force ther error on the receiver's side
                            frame.StopBits = allStopBitValues.Where(x => x != frame.StopBits).ElementAt(rng.Next(allStopBitValues.Length - 1));
                        }

                        recipient.GetMachine().HandleTimeDomainEvent(frameRecipient.WriteChar, obj, frame, when, () =>
                        {
                            DataRouted?.Invoke(sender, recipient, obj);
                        });
                    }
                    else
                    {
                        if(!StrictMode || compatible)
                        {
                            recipient.GetMachine().HandleTimeDomainEvent(recipient.WriteChar, obj, when, () =>
                            {
                                DataRouted?.Invoke(sender, recipient, obj);
                            });
                        }
                        else
                        {
                            var logLevel = hasWarnedForConnection.Contains(Tuple.Create(sender, recipient)) ? LogLevel.Debug : LogLevel.Warning;
                            this.Log(logLevel, "Strict mode enabled and reciver does not implement IUARTWithFrameInfo, dropping message ({0} to {1})", sender.GetName(), recipient.GetName());
                        }
                    }
                }
            }
        }

        private bool CheckUARTCompatibility(I sender, I recipient)
        {
            var result = true;
            // Only warn once for given connection. Not perfect as the UART's might be reconfigured during runtime
            var logLevel = hasWarnedForConnection.Contains(Tuple.Create(sender, recipient)) ? LogLevel.Debug : LogLevel.Warning;
            if(sender.BaudRate != recipient.BaudRate)
            {
                this.Log(logLevel, "Uart sender's ({0}) baudrate does not match reciver's ({1}). ({2} != {3})", sender.GetName(), recipient.GetName(), sender.BaudRate, recipient.BaudRate);
                result = false;
            }
            if(sender.ParityBit != Parity.Unsupported && recipient.ParityBit != Parity.Unsupported && sender.ParityBit != recipient.ParityBit)
            {
                this.Log(logLevel, "Uart sender's ({0}) parity bit mode does not match reciver's ({1}). ({2} != {3})", sender.GetName(), recipient.GetName(), sender.ParityBit, recipient.ParityBit);
                result = false;
            }
            if(sender.StopBits != recipient.StopBits)
            {
                this.Log(logLevel, "Uart sender's ({0}) stop bits mode does not match reciver's ({1}). ({2} != {3})", sender.GetName(), recipient.GetName(), sender.StopBits, recipient.StopBits);
                result = false;
            }
            if(!result)
            {
                hasWarnedForConnection.Add(Tuple.Create(sender, recipient));
            }
            return result;
        }

        [PostDeserialization]
        private void ReattachUARTsAfterDeserialization()
        {
            lock(locker)
            {
                foreach(var uart in uarts)
                {
                    uart.Key.CharReceived += uart.Value;
                }
            }
        }

        private double ValidateRate(string name, double value)
        {
            if(value < 0 || value > 1)
            {
                throw new RecoverableException($"{name} value has to be in range of [0;1], but received a value of: {value}");
            }
            return value;
        }

        private TData FlipBits<TData>(PseudorandomNumberGenerator rng, TData frame, out bool flipParity)
            where TData : IBinaryInteger<TData>
        {
            if(BitFlipRate == 0)
            {
                flipParity = false;
                return frame;
            }

            var bitCount = frame.GetByteCount() * 8;
            var maxFlips = rng.Next(0, Math.Min(bitCount, MaximumFlippedBits) + 1);
            var mask = TData.Zero;
            var positionsToFlip = maxFlips > 64 ? new byte[maxFlips] : stackalloc byte[maxFlips];
            rng.GetItems(allBitPositions, positionsToFlip);

            foreach(var bit in positionsToFlip)
            {
                mask |= (rng.NextDouble() < BitFlipRate ? TData.One : TData.Zero) << bit;
            }
            flipParity = TData.PopCount(mask) < TData.CreateChecked(maxFlips);
            return frame ^ mask;
        }

        private double bitFlipRate;
        private int maximumFlippedBits;
        private double droppedCharacterRate;
        private double frameErrorRate;
    }
}
