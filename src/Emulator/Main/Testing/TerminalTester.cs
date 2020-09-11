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
        public TerminalTester(TimeInterval? timeout = null, EndLineOption endLineOption = EndLineOption.TreatLineFeedAsEndLine, bool removeColors = true)
        {
            GlobalTimeout = timeout ?? TimeInterval.FromSeconds(DefaultSecondsTimeout);
            this.endLineOption = endLineOption;
            this.removeColors = removeColors;
            charLock = new object();
            lines = new List<Line>();
            currentLineBuffer = new StringBuilder();
            sgrDecodingBuffer = new StringBuilder();
            report = new StringBuilder();
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
            lock(charLock)
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
                    lines.Add(new Line(currentLineBuffer.ToString(), machine.ElapsedVirtualTime.TimeElapsed.TotalMilliseconds));
                    currentLineBuffer.Clear();
                }
#if DEBUG_EVENTS
                this.Log(LogLevel.Noisy, "Char received {0} (hex 0x{1:X})", (char)value, value);
#endif
                Monitor.Pulse(charLock);
            }
        }

        public TerminalTesterResult WaitFor(string pattern, TimeInterval? timeInterval = null, bool treatAsRegex = false, bool includeUnfinishedLine = false)
        {
            var eventName = "Line containing{1} >>{0}<<".FormatWith(pattern, treatAsRegex ? " regex" : string.Empty);
            var timeoutMilliseconds = GetTimeoutInMilliseconds(timeInterval);
#if DEBUG_EVENTS
            this.Log(LogLevel.Nois, "Waiting for a line containing >>{0}<< (include unfinished line: {1}, with timeout {2} ms, regex {3}) ", pattern, includeUnfinishedLine, timeoutMilliseconds, treatAsRegex);
#endif

            lock(charLock)
            {
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

                }, timeoutMilliseconds);

                if(result == null)
                {
                    HandleFailure(eventName);
                }
                return result;
            }
        }

        public TerminalTesterResult NextLine(TimeInterval? timeInterval = null)
        {
            lock(charLock)
            {
                var result = WaitForMatch(() =>
                {
                    if(!lines.Any())
                    {
                        return null;
                    }

                    return HandleSuccess("Next line", matchingLineId: 0);
                }, GetTimeoutInMilliseconds(timeInterval));

                if(result == null)
                {
                    HandleFailure("Next line");
                }
                return result;
            }
        }

        public bool IsIdle(TimeInterval? timeInterval = null)
        {
            lock(charLock)
            {
                var result = !Monitor.Wait(charLock, GetTimeoutInMilliseconds(timeInterval));
                if(!result)
                {
                    HandleFailure("Terminal is idle");
                }
                return result;
            }
        }

        public void ClearReport()
        {
            lines.Clear();
            report.Clear();
            reportClosed = false;
        }

        public string GetReport()
        {
            if(!reportClosed)
            {
                reportClosed = true;

                if(sgrDecodingBuffer.Length > 0)
                {
                    report.AppendFormat("--- SGR decoding buffer contains {0} characters: >>{1}<<\n", sgrDecodingBuffer.Length, sgrDecodingBuffer.ToString());
                }

                if(currentLineBuffer.Length > 0)
                {
                    report.AppendFormat("--- Current line buffer contains {0} characters: >>{1}<<\n", currentLineBuffer.Length, currentLineBuffer.ToString());
                }
            }

            return report.ToString();
        }

        public TimeInterval GlobalTimeout { get; set; }
        public TimeSpan WriteCharDelay { get; set; }

        private TerminalTesterResult WaitForMatch(Func<TerminalTesterResult> matchResult, int millisecondsTimeout)
        {
            var timeoutLeft = millisecondsTimeout;
            var swatch = new Stopwatch();
            while(timeoutLeft > 0)
            {
                var result = matchResult.Invoke();
#if DEBUG_EVENTS
                this.Log(LogLevel.Noisy, "Matching result: {0}", result);
#endif
                if(result != null)
                {
                    return result;
                }
                swatch.Restart();
#if DEBUG_EVENTS
                this.Log(LogLevel.Noisy, "Waiting for the next event");
#endif
                Monitor.Wait(charLock, timeoutLeft);
                swatch.Stop();
                timeoutLeft -= checked((int)swatch.ElapsedMilliseconds);
            }

#if DEBUG_EVENTS
            this.Log(LogLevel.Noisy, "Matching timeout");
#endif
            return null;
        }

        private TerminalTesterResult CheckFinishedLines(string pattern, bool regex, string eventName)
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
            if(abort)
            {
                currentLineBuffer.Append(sgrDecodingBuffer.ToString());
            }

            sgrDecodingBuffer.Clear();
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
                content = currentLineBuffer.ToString();
                timestamp = machine.ElapsedVirtualTime.TimeElapsed.TotalMilliseconds;

                currentLineBuffer.Clear();
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

        private void HandleFailure(string eventName)
        {
            ReportInner(eventName, "failure", lines.Count, true);

            currentLineBuffer.Clear();
            lines.Clear();
        }

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

        private int GetTimeoutInMilliseconds(TimeInterval? timeInterval = null)
        {
            return (int)(timeInterval.HasValue ? timeInterval.Value.TotalMilliseconds : GlobalTimeout.TotalMilliseconds);
        }

        private Machine machine;
        private SGRDecodingState sgrDecodingState;
        private bool reportClosed;

        private readonly object charLock;
        private readonly StringBuilder currentLineBuffer;
        private readonly StringBuilder sgrDecodingBuffer;
        private readonly EndLineOption endLineOption;
        private readonly bool removeColors;
        private readonly List<Line> lines;
        private readonly StringBuilder report;

        private const int DefaultSecondsTimeout = 8;
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

