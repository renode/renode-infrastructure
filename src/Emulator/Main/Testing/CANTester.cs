//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Antmicro.Migrant;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.CAN;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.CAN;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Testing
{
    public class CANTester : ICAN, IExternal
    {
        public CANTester(TimeInterval defaultTimeout)
        {
            this.defaultTimeout = defaultTimeout;
            frames = new List<CANMessageFrame>();
            matchedEvent = new AutoResetEvent(false);
            pauseEmulation = true;
        }

        public void OnFrameReceived(CANMessageFrame frame)
        {
            lock(frames)
            {
                frames.Add(frame);
            }
            if(pendingMatch is not null && pendingMatch.IsFrameMatching(frame))
            {
                if(pauseEmulation)
                {
                    EmulationManager.Instance.CurrentEmulation.PauseAll();
                }
                matchedFrame = frame;
                matchedEvent.Set();
            }
        }

        public bool SendISOTPMessage(uint senderId, uint receivingId, byte[] data, TimeInterval? timeout = null)
        {
            if(data.Length <= 7)
            {
                // Message fits in a single message
                var firstByte = (byte)data.Length;
                var frame = new CANMessageFrame(senderId, data.Prepend(firstByte).ToArray());
                SendFrame(frame);
                return true;
            }
            else
            {
                // Message is too long, needs to be sent as multiple frames
                var length = (ushort)data.Length;
                // First byte has a high nibble of 0x1, and a low nibble of the topmost 4 bits of the length
                var firstByte = (byte)((0x1 << 4) | BitHelper.GetValue(length, 8, 4));
                // Second byte is the rest of the length
                var secondByte = (byte)BitHelper.GetValue(length, 0, 8);

                var frame = new CANMessageFrame(senderId, data.Take(6).Prepend(new byte[] {firstByte, secondByte}).ToArray());
                SendFrame(frame);
                // Wait for the flow control frame
                frame = WaitForMessageFrame(new CANMatcher(singleId: receivingId, isotpType: ISOTP_PCI.FlowControlFrame), true, timeout: timeout);
                if(frame is null)
                {
                    this.ErrorLog("No flow control frame received");
                    return false;
                }
                if(BitHelper.GetValue(frame.Data[0], 0, 4) != 0)
                {
                    throw new NotImplementedException("CANTester does not support ISOTP blocksize != 0");
                }
                var emulation = EmulationManager.Instance.CurrentEmulation;
                var transmissionDoneHandle = new AutoResetEvent(false);
                // No matching frames received so setup for waiting
                var timeoutEvent = emulation.MasterTimeSource.EnqueueTimeoutEvent(((ulong)(timeout ?? defaultTimeout).TotalMilliseconds), () =>
                {
                    if(pauseEmulation)
                    {
                        emulation.PauseAll();
                    }
                });

                var now = TimeDomainsManager.Instance.GetEffectiveVirtualTimeStamp();
                // Time between frames is roughly based on a CAN bus bit rate of 500kbit/s

                // First 6 bytes are already transmitted as part of the first message
                var frameIndex = 1;
                for(var i = 6; i < data.Length; i += 7)
                {
                    var frameTime = TimeInterval.FromMicroseconds(200*(((ulong)i/7)+1));
                    if(i + 8 < data.Length)
                    {
                        // More than one frame left
                        var bytes = data.Skip(i).Take(7).Prepend((byte)(((byte)ISOTP_PCI.ConsecutiveFrame << 4) | frameIndex));
                        emulation.MasterTimeSource.ExecuteInSyncedState((_) =>
                        {
                            SendFrame(new CANMessageFrame(senderId, bytes.ToArray()));
                        }, new TimeStamp(now.TimeElapsed + frameTime, now.Domain));
                        this.InfoLog("Queueing bytes {0}-{1} for transmission at {2}", i, i + 7, now.TimeElapsed + frameTime);
                    }
                    else
                    {
                        // Final frame
                        var bytes = data.Skip(i).Take(7).Prepend((byte)(((byte)ISOTP_PCI.ConsecutiveFrame << 4) | frameIndex));
                        emulation.MasterTimeSource.ExecuteInSyncedState((_) =>
                        {
                            SendFrame(new CANMessageFrame(senderId, bytes.ToArray()));
                            timeoutEvent.Cancel();
                            transmissionDoneHandle.Set();
                        }, new TimeStamp(now.TimeElapsed + frameTime, now.Domain));
                        this.InfoLog("Queueing bytes {0}-{1} for transmission at {2}", i, data.Length, now.TimeElapsed + frameTime);
                    }
                    frameIndex++;
                }

                if(!emulation.IsStarted)
                {
                    emulation.StartAll();
                }
                this.InfoLog("All messages queued, waiting for completion or timeout");

                if(WaitHandle.WaitAny(new[] { timeoutEvent.WaitHandle, transmissionDoneHandle }) == 1)
                {
                    this.InfoLog("All messages transmitted");
                    return true;
                }
                else
                {
                    // Timeout trying to transmit
                    this.ErrorLog("Timed out trying to send multi-frame message");
                    return false;
                }
            }
        }

        public List<byte> WaitForISOTPMessage(uint sendingId, uint receivingId, bool pauseEmulation = true, bool keep = false, TimeInterval? timeout = null)
        {
            var data = new List<byte>();
            var filter = new CANMatcher(receivingId);
            var frame = WaitForMessageFrame(filter, pauseEmulation, keep, timeout);
            if(frame is null)
            {
                // Timeout waiting for first frame
                return null;
            }
            switch((ISOTP_PCI)BitHelper.GetValue(frame.Data[0], 4, 4))
            {
            case ISOTP_PCI.SingleFrame:
                // Only a single frame, so we can just extract data and return
                return frame.Data.Skip(1).ToList();
            case ISOTP_PCI.FirstFrame:
                // Multi frame message
                // Length is the low nibble of the first byte, and the full second byte
                var length = (BitHelper.GetValue(frame.Data[0], 0, 4) << 8) | frame.Data[1];
                // Save the first part of the data
                data.AddRange(frame.Data.Skip(2));
                // Send a flow control frame to continue the transmission without delays
                SendFrame(new CANMessageFrame(sendingId, [0x30, 0, 0, 0, 0, 0, 0, 0]));
                // Loop until all bytes have been received
                var index = 0;
                for(var remainingBytes = length - 6; remainingBytes > 0;)
                {
                    frame = WaitForMessageFrame(filter, pauseEmulation, keep, timeout);
                    if(frame is null)
                    {
                        this.ErrorLog("Timed out waiting for a consecutive ISOTP frame");
                        return null;
                    }
                    index++;
                    if((ISOTP_PCI)BitHelper.GetValue(frame.Data[0], 4, 4) != ISOTP_PCI.ConsecutiveFrame)
                    {
                        this.ErrorLog("Incorrect ISOTP PCI mid sequence");
                        return null;
                    }
                    if(index != BitHelper.GetValue(frame.Data[0], 0, 4))
                    {
                        this.ErrorLog("Out of sequence ISOTP frame received");
                        return null;
                    }
                    if(remainingBytes >= 7)
                    {
                        data.AddRange(frame.Data.Skip(1));
                        remainingBytes -= 7;
                    }
                    else
                    {
                        // final frame
                        data.AddRange(frame.Data.Skip(1).Take(remainingBytes));
                        if(data.Count != length)
                        {
                            this.ErrorLog("ISOTP payload size mismatch, reported: {0}, actual: {1}", length, data.Count);
                            return null;
                        }
                        return data;
                    }
                }
                return null;
            default:
                // Invalid first frame
                this.ErrorLog("Unexpected ISOTP message type {0}", (ISOTP_PCI)BitHelper.GetValue(frame.Data[0], 4, 4));
                return null;
            }
        }

        public void SendFrame(CANMessageFrame frame)
        {
            var emulation = EmulationManager.Instance.CurrentEmulation;
            if(!emulation.IsStarted)
            {
                emulation.StartAll();
            }
            FrameSent?.Invoke(frame);
        }

        public CANMessageFrame WaitForMessageFrame(CANMatcher filter, bool pauseEmulation = true, bool keep = false, TimeInterval? timeout = null)
        {
            var emulation = EmulationManager.Instance.CurrentEmulation;
            this.pauseEmulation = pauseEmulation;
            // First check if the message has already been received
            lock(frames)
            {
                for(var i = 0; i < frames.Count; i++)
                {
                    var frame = frames[i];
                    if(filter.IsFrameMatching(frame))
                    {
                        // Match found so we shortcut
                        if(pauseEmulation)
                        {
                            emulation.PauseAll();
                        }
                        if(!keep)
                        {
                            // Consume up to and including the matched frame, otherwise a subsequent wait would return this same (now stale) frame again
                            frames.RemoveRange(0, i + 1);
                        }
                        return frame;
                    }
                }
            }
            // No matching frames received so setup for waiting
            var timeoutEvent = emulation.MasterTimeSource.EnqueueTimeoutEvent(((ulong)(timeout ?? defaultTimeout).TotalMilliseconds), () =>
            {
                if(pauseEmulation)
                {
                    emulation.PauseAll();
                }
            });
            // Drop any signal left over from a previous timeouted wait
            matchedEvent.Reset();
            pendingMatch = filter;
            // If emulation is paused, resume it
            if(!emulation.IsStarted)
            {
                emulation.StartAll();
            }

            if(WaitHandle.WaitAny(new[] { timeoutEvent.WaitHandle, matchedEvent }) == 1)
            {
                // Match event, emulation is already paused if needed
                if(!keep)
                {
                    lock(frames)
                    {
                        frames.Clear();
                    }
                }
                timeoutEvent.Cancel();
                pendingMatch = null;
                return matchedFrame;
            }
            else
            {
                // Timeout event
                pendingMatch = null;
                return null;
            }
        }

        public void Reset()
        {
            lock(frames)
            {
                frames.Clear();
            }
            matchedEvent.Reset();
            pendingMatch = null;
        }

        public event Action<CANMessageFrame> FrameSent;

        private CANMessageFrame matchedFrame;
        private CANMatcher pendingMatch;
        private bool pauseEmulation;

        private readonly List<CANMessageFrame> frames;

        [Constructor(false)]
        private readonly AutoResetEvent matchedEvent;

        private readonly TimeInterval defaultTimeout;

        public class CANMatcher(uint? singleId = null, ISOTP_PCI? isotpType = null)
        {
            public bool IsFrameMatching(CANMessageFrame frame)
            {
                // All enabled (non-null) filters must match for the whole filter to match
                if(singleId is not null)
                {
                    if(singleId != frame.Id)
                    {
                        return false;
                    }
                }

                if(isotpType is not null)
                {
                    if((ISOTP_PCI)BitHelper.GetValue(frame.Data[0], 4, 4) != isotpType)
                    {
                        return false;
                    }
                }

                return true;
            }

            // ID filters
            private readonly uint? singleId = singleId;
            // ISO-TP Type
            private readonly ISOTP_PCI? isotpType = isotpType;
        }

        public enum ISOTP_PCI
        {
            SingleFrame = 0x0,
            FirstFrame = 0x1,
            ConsecutiveFrame = 0x2,
            FlowControlFrame = 0x3
        }
    }
}
