//
// Copyright (c) 2010-2018 Antmicro
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

            ReportSuccess("Attached to UART");
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

        public TerminalTesterResult WaitFor(string pattern, TimeInterval? timeInterval = null, bool treatAsRegex = false, bool charByCharEnabled = false)
        {
            lock(charLock)
            {
                var timeoutMiliseconds = (int)(timeInterval.HasValue ? timeInterval.Value.TotalMilliseconds : GlobalTimeout.TotalMilliseconds);
                return treatAsRegex
                    ? RegexMatch(pattern, timeoutMiliseconds, charByCharEnabled)
                    : Contains(pattern, timeoutMiliseconds, charByCharEnabled);
            }
        }

        public TerminalTesterResult NextLine(TimeInterval? timeInterval = null)
        {
            lock(charLock)
            {
                var timeoutMiliseconds = (int)(timeInterval.HasValue ? timeInterval.Value.TotalMilliseconds : GlobalTimeout.TotalMilliseconds);
                var result = WaitForMatch(() =>
                {
                    if(!lines.Any())
                    {
                        return null;
                    }
                    var nextLine = lines.First();
                    ReportSuccess("Next line", copyLinesToReport: 1);

                    lines.RemoveAt(0);
                    return new TerminalTesterResult(nextLine.Content, nextLine.VirtualTimestamp);
                }, timeoutMiliseconds);

                if(result == null)
                {
                    ReportFailure("Next line");
                }
                return result;
            }
        }

        public bool IsIdle(TimeInterval? timeInterval = null)
        {
            lock(charLock)
            {
                var timeoutMiliseconds = (int)(timeInterval.HasValue ? timeInterval.Value.TotalMilliseconds : GlobalTimeout.TotalMilliseconds);
                var result = !Monitor.Wait(charLock, timeoutMiliseconds);
                if(!result)
                {
                    ReportFailure("Terminal is idle", copyLinesToReport: -1);
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

        private TerminalTesterResult Contains(string pattern, int milisecondsTimeout, bool charByCharEnabled)
        {
#if DEBUG_EVENTS
            this.Log(LogLevel.Noisy, "Waiting for a line containing >>{0}<< (char by char: {1}, with timeout {2} ms) ", pattern, charByCharEnabled, milisecondsTimeout);
#endif
            var result = WaitForMatch(() =>
            {
                var lineMatch = MatchByLine(pattern);
                if(lineMatch != null)
                {
                    return lineMatch;
                }

                if(!charByCharEnabled)
                {
                    return null;
                }

                // check current line buffer only in char-by-char mode
                var bufferText = currentLineBuffer.ToString();
#if DEBUG_EVENTS
                this.Log(LogLevel.Noisy, "Buffer content >>{0}<<", bufferText);
#endif
                if(bufferText.Contains(pattern))
                {
                    ReportSuccess("Line containing >>{0}<<".FormatWith(pattern), copyLinesToReport: -1, includeCurrentLineBuffer: true);

                    lines.Clear();
                    currentLineBuffer.Clear();
                    return new TerminalTesterResult(bufferText, machine.ElapsedVirtualTime.TimeElapsed.TotalMilliseconds);
                }
                return null;
            }, milisecondsTimeout);

            if(result == null)
            {
                ReportFailure("Line containing >>{0}<<".FormatWith(pattern), copyLinesToReport: -1);
            }
            return result;
        }

        private TerminalTesterResult RegexMatch(string pattern, int milisecondsTimeout, bool charByCharEnabled)
        {
            var result = WaitForMatch(() =>
            {
                var lineMatch = RegexMatchByLine(pattern);
                if(lineMatch != null)
                {
                    return lineMatch;
                }

                if(!charByCharEnabled)
                {
                    return null;
                }

                var content = currentLineBuffer.ToString();
                var match = Regex.Match(content, pattern);
                if(match.Success)
                {
                    ReportSuccess("Line containing regex >>{0}<<".FormatWith(pattern), copyLinesToReport: -1, includeCurrentLineBuffer: true);

                    lines.Clear();
                    currentLineBuffer.Clear();
                    return new TerminalTesterResult(content, machine.ElapsedVirtualTime.TimeElapsed.TotalMilliseconds, GetMatchGroups(match));
                }
                return null;
            }, milisecondsTimeout);

            if(result == null)
            {
                ReportFailure("Line containing regex >>{0}<<".FormatWith(pattern), copyLinesToReport: -1);
                lines.Clear();
            }
            return result;
        }

        private TerminalTesterResult WaitForMatch(Func<TerminalTesterResult> matchResult, int milisecondsTimeout)
        {
            var timeoutLeft = milisecondsTimeout;
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

        private TerminalTesterResult MatchByLine(string pattern)
        {
            var index = lines.FindIndex(x => x.Content.Contains(pattern));
            if(index != -1)
            {
                ReportSuccess("Line containing >>{0}<<".FormatWith(pattern), copyLinesToReport: index + 1);

                var result = new TerminalTesterResult(lines[index].Content, lines[index].VirtualTimestamp);
                lines.RemoveRange(0, index + 1);
                return result;
            }

            return null;
        }

        private TerminalTesterResult RegexMatchByLine(string pattern)
        {
            var groups = new string[0];
            var index = lines.FindIndex(x =>
            {
                var match = Regex.Match(x.Content, pattern);
                if(match.Success)
                {
                    groups = GetMatchGroups(match);
                }
                return match.Success;
            });

            if(index != -1)
            {
                ReportSuccess("Line containing regex >>{0}<<".FormatWith(pattern), copyLinesToReport: index + 1);

                var result = new TerminalTesterResult(lines[index].Content, lines[index].VirtualTimestamp, groups);
                lines.RemoveRange(0, index + 1);
                return result;
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
            return match.Groups.Cast<Group>().Skip(1).Select(y => y.Value).ToArray();
        }

        private void FinishSGRDecoding(bool abort = false)
        {
#if DEBUG_EVENTS
            this.Log(LogLevel.Noisy, "Finishing GSR decoding (abort: {0}, filterBuffer: >>{1}<<)", abort, sgrDecodingBuffer.ToString());
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

        private void ReportSuccess(string eventName, int copyLinesToReport = 0, bool includeCurrentLineBuffer = false)
        {
            ReportInner(eventName, "success", copyLinesToReport, includeCurrentLineBuffer);
        }

        private void ReportFailure(string eventName, int copyLinesToReport = 0, bool includeCurrentLineBuffer = false)
        {
            ReportInner(eventName, "failure", copyLinesToReport, includeCurrentLineBuffer);
        }

        private void ReportInner(string eventName, string what, int copyLinesToReport = 0, bool includeCurrentLineBuffer = false)
        {
            if(copyLinesToReport == -1)
            {
                copyLinesToReport = lines.Count;
            }

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

            report.AppendFormat("([host: {2}] {0} event: {1})\n", eventName, what, CustomDateTime.Now);
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

        private const int DefaultSecondsTimeout = 30;
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

