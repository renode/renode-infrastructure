//
// Copyright (c) 2010-2020 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

// Uncomment the following line to enable events debugging
// #define DEBUG_EVENTS

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Antmicro.Renode.Backends.Terminals;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.UART;
using Antmicro.Renode.Time;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Testing
{
    public class TerminalTester : BackendTerminal
    {
        public TerminalTester(TimeInterval timeout, EndLineOption endLineOption = EndLineOption.TreatLineFeedAsEndLine, bool removeColors = true)
        {
            GlobalTimeout = timeout;
            this.endLineOption = endLineOption;
            this.removeColors = removeColors;
            charEvent = new AutoResetEvent(false);
            lines = new List<Line>();
            currentLineBuffer = new SafeStringBuilder();
            sgrDecodingBuffer = new SafeStringBuilder();
            report = new SafeStringBuilder();
        }

        public override void AttachTo(IUART uart)
        {
            machine = uart.GetMachine();
            if(machine == null)
            {
                throw new ArgumentException("Could not find machine for UART");
            }
            base.AttachTo(uart);

            HandleSuccess("Attached to UART", matchingLineId: NoLine);
        }

        public void Write(string text)
        {
            foreach(var chr in text)
            {
                CallCharReceived((byte)chr);
                WaitBeforeNextChar();
            }
        }

        public void WriteLine(string line = "")
        {
            Write(line);
            CallCharReceived(CarriageReturn);
            WaitBeforeNextChar();
        }

        public override void WriteChar(byte value)
        {
            if(value == CarriageReturn && endLineOption == EndLineOption.TreatLineFeedAsEndLine)
            {
                return;
            }

            if(value != (endLineOption == EndLineOption.TreatLineFeedAsEndLine ? LineFeed : CarriageReturn))
            {
                AppendCharToBuffer((char)value);
            }
            else
            {
                var line = currentLineBuffer.Unload();
                lock(lines)
                {
                    lines.Add(new Line(line, machine.ElapsedVirtualTime.TimeElapsed.TotalMilliseconds));
                }
            }

#if DEBUG_EVENTS
            this.Log(LogLevel.Noisy, "Char received {0} (hex 0x{1:X})", (char)value, value);
#endif
            charEvent.Set();
        }

        public TerminalTesterResult WaitFor(string pattern, TimeInterval? timeout = null, bool treatAsRegex = false, bool includeUnfinishedLine = false)
        {
            var eventName = "Line containing{1} >>{0}<<".FormatWith(pattern, treatAsRegex ? " regex" : string.Empty);
#if DEBUG_EVENTS
            this.Log(LogLevel.Noisy, "Waiting for a line containing >>{0}<< (include unfinished line: {1}, with timeout {2}, regex {3}) ", pattern, includeUnfinishedLine, timeout ?? GlobalTimeout, treatAsRegex);
#endif

            var result = WaitForMatch(() =>
            {
                var lineMatch = CheckFinishedLines(pattern, treatAsRegex, eventName);
                if(lineMatch != null)
                {
                    return lineMatch;
                }

                if(!includeUnfinishedLine)
                {
                    return null;
                }

                return CheckUnfinishedLine(pattern, treatAsRegex, eventName);

            }, timeout ?? GlobalTimeout);

            if(result == null)
            {
                HandleFailure(eventName);
            }
            return result;
        }

        public TerminalTesterResult NextLine(TimeInterval? timeout = null)
        {
            var result = WaitForMatch(() =>
            {
                if(lines.Count == 0)
                {
                    return null;
                }

                return HandleSuccess("Next line", matchingLineId: 0);
            }, timeout ?? GlobalTimeout);

            if(result == null)
            {
                HandleFailure("Next line");
            }
            return result;
        }

        public bool IsIdle(TimeInterval? timeout = null)
        {
            var timeoutEvent = machine.LocalTimeSource.EnqueueTimeoutEvent((ulong)((timeout ?? GlobalTimeout).TotalMilliseconds));
            var waitHandles = new [] { charEvent, timeoutEvent.WaitHandle };

            charEvent.Reset();
            WaitHandle.WaitAny(waitHandles);
            var result = timeoutEvent.IsTriggered;
            if(!result)
            {
                HandleFailure("Terminal is idle");
            }
            return result;
        }

        public void ClearReport()
        {
            lock(lines)
            {
                lines.Clear();
                report.Unload();
                generatedReport = null;
            }
        }

        public string GetReport()
        {
            if(generatedReport == null)
            {
                if(sgrDecodingBuffer.TryDump(out var sgr))
                {
                    report.AppendFormat("--- SGR decoding buffer contains {0} characters: >>{1}<<\n", sgr.Length, sgr);
                }

                if(currentLineBuffer.TryDump(out var line))
                {
                    report.AppendFormat("--- Current line buffer contains {0} characters: >>{1}<<\n", line.Length, line);
                }

                generatedReport = report.Unload();
            }

            return generatedReport;
        }

        public TimeInterval GlobalTimeout { get; set; }
        public TimeSpan WriteCharDelay { get; set; }

        private TerminalTesterResult WaitForMatch(Func<TerminalTesterResult> matchResult, TimeInterval timeout)
        {
            var timeoutEvent = machine.LocalTimeSource.EnqueueTimeoutEvent((ulong)timeout.TotalMilliseconds);
            var waitHandles = new [] { charEvent, timeoutEvent.WaitHandle };

            do
            {
                var result = matchResult.Invoke();
#if DEBUG_EVENTS
                this.Log(LogLevel.Noisy, "Matching result: {0}", result);
#endif
                if(result != null)
                {
                    return result;
                }
#if DEBUG_EVENTS
                this.Log(LogLevel.Noisy, "Waiting for the next event");
#endif
                WaitHandle.WaitAny(waitHandles);
            }
            while(!timeoutEvent.IsTriggered);

#if DEBUG_EVENTS
            this.Log(LogLevel.Noisy, "Matching timeout");
#endif
            return null;
        }

        private TerminalTesterResult CheckFinishedLines(string pattern, bool regex, string eventName)
        {
            lock(lines)
            {
                string[] matchGroups = null;
                var index = regex
                    ? lines.FindIndex(x =>
                        {
                            var match = Regex.Match(x.Content, pattern);
                            if(match.Success)
                            {
                                matchGroups = GetMatchGroups(match);
                            }
                            return match.Success;
                        })
                    : lines.FindIndex(x => x.Content.Contains(pattern));

                if(index != -1)
                {
                    return HandleSuccess(eventName, matchingLineId: index, matchGroups: matchGroups);
                }

                return null;
            }
        }

        private TerminalTesterResult CheckUnfinishedLine(string pattern, bool regex, string eventName)
        {
            var content = currentLineBuffer.ToString();

#if DEBUG_EVENTS
            this.Log(LogLevel.Noisy, "Current line buffer content: >>{0}<<", content);
#endif

            var isMatch = false;
            string[] matchGroups = null;

            if(regex)
            {
                var match = Regex.Match(content, pattern);
                isMatch = match.Success;
                matchGroups = GetMatchGroups(match);
            }
            else
            {
                isMatch = content.Contains(pattern);
            }

            if(isMatch)
            {
                return HandleSuccess(eventName, matchingLineId: CurrentLine, matchGroups: matchGroups);
            }

            return null;
        }

        private void WaitBeforeNextChar()
        {
            if(WriteCharDelay != TimeSpan.Zero)
            {
                Thread.Sleep(WriteCharDelay);
            }
        }

        private string[] GetMatchGroups(Match match)
        {
            // group '0' is the whole match; we return it in a separate field, so we don't want it here
            return match.Groups.Cast<Group>().Skip(1).Select(y => y.Value).ToArray();
        }

        private void FinishSGRDecoding(bool abort = false)
        {
#if DEBUG_EVENTS
            this.Log(LogLevel.Noisy, "Finishing SGR decoding (abort: {0}, filterBuffer: >>{1}<<)", abort, sgrDecodingBuffer.ToString());
#endif
            var sgr = sgrDecodingBuffer.Unload();
            if(abort)
            {
                currentLineBuffer.Append(sgr);
            }

            sgrDecodingState = SGRDecodingState.NotDecoding;
        }

        private void AppendCharToBuffer(char value)
        {
#if DEBUG_EVENTS
            this.Log(LogLevel.Noisy, "Appending char >>{0}<< to buffer in state {1}", value, sgrDecodingState);
#endif
            if(!removeColors)
            {
                currentLineBuffer.Append(value);
                return;
            }

            switch(sgrDecodingState)
            {
                case SGRDecodingState.NotDecoding:
                {
                    if(value == EscapeChar)
                    {
                        sgrDecodingBuffer.Append(value);
                        sgrDecodingState = SGRDecodingState.EscapeDetected;
                    }
                    else
                    {
                        currentLineBuffer.Append(value);
                    }
                }
                break;

                case SGRDecodingState.EscapeDetected:
                {
                    if(value == '[')
                    {
                        sgrDecodingState = SGRDecodingState.LeftBracketDetected;
                    }
                    else
                    {
                        FinishSGRDecoding(abort: true);
                    }
                }
                break;

                case SGRDecodingState.LeftBracketDetected:
                {
                    if((value >= '0' && value <= '9') || value == ';')
                    {
                        sgrDecodingBuffer.Append(value);
                    }
                    else
                    {
                        FinishSGRDecoding(abort: value != 'm');
                    }
                    break;
                }

                default:
                    throw new ArgumentException($"Unexpected state when decoding an SGR code: {sgrDecodingState}");
            }
        }

        private const int NoLine = -2;
        private const int CurrentLine = -1;

        private TerminalTesterResult HandleSuccess(string eventName, int matchingLineId, string[] matchGroups = null)
        {
            lock(lines)
            {
                var numberOfLinesToCopy = 0;
                var includeCurrentLineBuffer = false;
                switch(matchingLineId)
                {
                    case NoLine:
                        // default values are ok
                        break;

                    case CurrentLine:
                        includeCurrentLineBuffer = true;
                        numberOfLinesToCopy = lines.Count;
                        break;

                    default:
                        numberOfLinesToCopy = matchingLineId + 1;
                        break;
                }

                ReportInner(eventName, "success", numberOfLinesToCopy, includeCurrentLineBuffer);

                string content = null;
                double timestamp = 0;
                if(includeCurrentLineBuffer)
                {
                    timestamp = machine.ElapsedVirtualTime.TimeElapsed.TotalMilliseconds;

                    content = currentLineBuffer.Unload();
                }
                else if(numberOfLinesToCopy > 0)
                {
                    var item = lines[matchingLineId];
                    content = item.Content;
                    timestamp = item.VirtualTimestamp;
                }

                lines.RemoveRange(0, numberOfLinesToCopy);

                return new TerminalTesterResult(content, timestamp, matchGroups);
            }
        }

        private void HandleFailure(string eventName)
        {
            lock(lines)
            {
                ReportInner(eventName, "failure", lines.Count, true);

                currentLineBuffer.Unload();
                lines.Clear();
            }
        }

        // does not need to be locked, as both `HandleSuccess` and `HandleFailure` use locks
        private void ReportInner(string eventName, string what, int copyLinesToReport, bool includeCurrentLineBuffer)
        {
            if(copyLinesToReport > 0)
            {
                foreach(var line in lines.Take(copyLinesToReport))
                {
                    report.AppendLine(line.ToString());
                }
            }

            if(includeCurrentLineBuffer)
            {
                report.AppendFormat("{0} [[no newline]]\n", currentLineBuffer);
            }

            var virtMs = machine.ElapsedVirtualTime.TimeElapsed.TotalMilliseconds;
            report.AppendFormat("([host: {2}, virt: {3, 7}] {0} event: {1})\n", eventName, what, CustomDateTime.Now, virtMs);
        }

        private Machine machine;
        private SGRDecodingState sgrDecodingState;
        private string generatedReport;

        private readonly AutoResetEvent charEvent;
        private readonly SafeStringBuilder currentLineBuffer;
        private readonly SafeStringBuilder sgrDecodingBuffer;
        private readonly EndLineOption endLineOption;
        private readonly bool removeColors;
        private readonly List<Line> lines;
        private readonly SafeStringBuilder report;

        private const byte LineFeed = 0xA;
        private const byte CarriageReturn = 0xD;
        private const char EscapeChar = '\x1B';

        private class Line
        {
            public Line(string content, double timestamp)
            {
                this.Content = content;
                this.VirtualTimestamp = timestamp;
                this.HostTimestamp = CustomDateTime.Now;
            }

            public override string ToString()
            {
                return $"[host: {HostTimestamp}, virt: {(int)VirtualTimestamp, 7}] {Content}";
            }

            public string Content { get; }
            public double VirtualTimestamp { get; }
            public DateTime HostTimestamp { get; }
        }

        private enum SGRDecodingState
        {
            NotDecoding,
            EscapeDetected,
            LeftBracketDetected
        }
    }

    public class TerminalTesterResult
    {
        public TerminalTesterResult(string content, double timestamp, string[] groups = null)
        {
            this.line = content == null ? string.Empty : content.StripNonSafeCharacters();
            this.timestamp = timestamp;
            this.groups = groups ?? new string[0];
        }

        public string line { get; }
        public double timestamp { get; }
        public string[] groups { get; set; }
    }

    public enum EndLineOption
    {
        TreatLineFeedAsEndLine,
        TreatCarriageReturnAsEndLine
    }
}

