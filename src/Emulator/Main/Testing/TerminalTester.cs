//
// Copyright (c) 2010-2025 Antmicro
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
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.UART;
using Antmicro.Renode.Time;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;

namespace Antmicro.Renode.Testing
{
    public class TerminalTester : BackendTerminal
    {
        public TerminalTester(TimeInterval timeout, EndLineOption endLineOption = EndLineOption.TreatLineFeedAsEndLine, bool binaryMode = false)
        {
            GlobalTimeout = timeout;
            this.endLineOption = endLineOption;
            this.binaryMode = binaryMode;
            charEvent = new AutoResetEvent(false);
            matchEvent = new AutoResetEvent(false);
            lines = new List<Line>();
            currentLineBuffer = new SafeStringBuilder();
            sgrDecodingBuffer = new SafeStringBuilder();
            report = new SafeStringBuilder();
            failingStrings = new List<FailingString>();
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
            if(WriteCharDelay != TimeSpan.Zero)
            {
                lock(delayedChars)
                {
                    foreach(var character in text)
                    {
                        delayedChars.Enqueue(Tuple.Create(WriteCharDelay, character));
                    }

                    if(!delayedCharInProgress)
                    {
                        HandleDelayedChars();
                    }
                }
            }
            else
            {
                foreach(var chr in text)
                {
                    CallCharReceived((byte)chr);
                }
            }
        }

        public void WriteLine(string line = "")
        {
            Write(line + CarriageReturn);
        }

        public override void WriteChar(byte value)
        {
            if(!binaryMode && value == CarriageReturn && endLineOption == EndLineOption.TreatLineFeedAsEndLine)
            {
                return;
            }

            if(binaryMode || value != (endLineOption == EndLineOption.TreatLineFeedAsEndLine ? LineFeed : CarriageReturn))
            {
                AppendCharToBuffer((char)value);
            }
            else
            {
                lock(lines)
                {
                    var line = currentLineBuffer.Unload();
                    lines.Add(new Line(line, machine.ElapsedVirtualTime.TimeElapsed.TotalMilliseconds));
                }
            }

#if DEBUG_EVENTS
            this.Log(LogLevel.Noisy, "Char received {0} (hex 0x{1:X})", (char)value, value);
#endif
            charEvent.Set();

            lock(lines)
            {
                // If we're not waiting for a match, we have nothing to do
                if(resultMatcher == null)
                {
                    return;
                }

                if(testerResult == null)
                {
                    // First check for failing strings
                    testerResult = MatchFailingStrings() ?? resultMatcher.Invoke();
#if DEBUG_EVENTS
                    this.Log(LogLevel.Noisy, "Matching result: {0}", testerResult);
#endif
                }
                // If there was no match, we just keep waiting
                if(testerResult != null)
                {
                    // Stop matching if we have already matched something
                    resultMatcher = null;

                    if(pauseEmulation)
                    {
                        machine.PauseAndRequestEmulationPause(precise: true);
                        pauseEmulation = false;
                    }

                    matchEvent.Set();
                    return;
                }
            }
        }

        public TerminalTesterResult WaitFor(string pattern, TimeInterval? timeout = null, bool treatAsRegex = false, bool includeUnfinishedLine = false, bool pauseEmulation = false, bool matchNextLine = false)
        {
            var eventName = "Line containing{1} >>{0}<<".FormatWith(pattern, treatAsRegex ? " regex" : string.Empty);
#if DEBUG_EVENTS
            this.Log(LogLevel.Noisy, "Waiting for a line containing >>{0}<< (include unfinished line: {1}, with timeout {2}, regex {3}, pause on match {4}, match next line {5}) ",
                pattern, includeUnfinishedLine, timeout ?? GlobalTimeout, treatAsRegex, pauseEmulation, matchNextLine);
#endif

            if(binaryMode && !treatAsRegex)
            {
                // The pattern is provided as a hex string. Parse it to a byte array and then decode
                // using the Latin1 encoding which passes through byte values as character values (that is,
                // { 0x00, 0x80, 0xff } becomes "\x00\x80\xff") to convert to a string where each char
                // is equivalent to one input byte.
                pattern = Encoding.GetEncoding("iso-8859-1").GetString(Misc.HexStringToByteArray(pattern, ignoreWhitespace: true));
            }

            var result = WaitForMatch(() =>
            {
                var lineMatch = CheckFinishedLines(pattern, treatAsRegex, eventName, matchNextLine);
                if(lineMatch != null)
                {
                    return lineMatch;
                }

                if(!includeUnfinishedLine)
                {
                    return null;
                }

                return CheckUnfinishedLine(pattern, treatAsRegex, eventName, matchAtStart: matchNextLine);
            }, timeout ?? GlobalTimeout, pauseEmulation);

            if(result == null)
            {
                HandleFailure(eventName);
            }
            return result;
        }

        public TerminalTesterResult WaitFor(string[] patterns, TimeInterval? timeout = null, bool treatAsRegex = false, bool includeUnfinishedLine = false, bool pauseEmulation = false, bool matchFromNextLine = false)
        {
            if(patterns.Length == 0)
            {
                return HandleSuccess("Empty match", matchingLineId: NoLine);
            }
#if DEBUG_EVENTS
            this.Log(LogLevel.Noisy, "Waiting for a multi-line match starting with >>{0}<< (include unfinished line: {1}, with timeout {2}, regex {3}, pause on match {4}, match from next line {5}) ",
                patterns[0], includeUnfinishedLine, timeout ?? GlobalTimeout, treatAsRegex, pauseEmulation, matchFromNextLine);
#endif
            if(!matchFromNextLine)
            {
                if(includeUnfinishedLine && patterns.Length == 1)
                {
                    return WaitFor(patterns[0], timeout, treatAsRegex, true, pauseEmulation);
                }
                return WaitForMultilineMatch(patterns, timeout, treatAsRegex, includeUnfinishedLine, pauseEmulation);
            }

            TerminalTesterResult result = null;
            for(var i = 0; i < patterns.Length; ++i)
            {
                result = WaitFor(patterns[i], timeout, treatAsRegex, includeUnfinishedLine && (i == patterns.Length - 1),
                    pauseEmulation, true);

                if(result == null)
                {
                   return null;
                }
            }
            return result;
        }

        public TerminalTesterResult NextLine(TimeInterval? timeout = null, bool pauseEmulation = false)
        {
            var result = WaitForMatch(() =>
            {
                if(lines.Count == 0)
                {
                    return null;
                }

                return HandleSuccess("Next line", matchingLineId: 0);
            }, timeout ?? GlobalTimeout, pauseEmulation);

            if(result == null)
            {
                HandleFailure("Next line");
            }
            return result;
        }

        public bool IsIdle(TimeInterval? timeout = null, bool pauseEmulation = true)
        {
            var emulation = EmulationManager.Instance.CurrentEmulation;
            this.pauseEmulation = pauseEmulation;
            var timeoutEvent = machine.LocalTimeSource.EnqueueTimeoutEvent(
                (ulong)(timeout ?? GlobalTimeout).TotalMilliseconds,
                () =>
                {
                    if(this.pauseEmulation)
                    {
                        emulation.PauseAll();
                        this.pauseEmulation = false;
                    }
                }
            );

            charEvent.Reset();
            if(!emulation.IsStarted)
            {
                emulation.StartAll();
            }

            var eventIdx = WaitHandle.WaitAny( new [] { timeoutEvent.WaitHandle, charEvent } );
            var result = eventIdx == 0;
            if(!result)
            {
                HandleFailure("Terminal is idle");
            }
            this.pauseEmulation = false;
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

        public void RegisterFailingString(string pattern, bool treatAsRegex)
        {
            failingStrings.Add(new FailingString(pattern, treatAsRegex));
        }

        public void UnregisterFailingString(string pattern, bool treatAsRegex)
        {
            if(!failingStrings.Remove(new FailingString(pattern, treatAsRegex)))
            {
                throw new RecoverableException("Unable to unregister failing string, entry not found");
            }
        }

        public TimeInterval GlobalTimeout { get; set; }
        public TimeSpan WriteCharDelay { get; set; }
        public bool BinaryMode => binaryMode;

        private void HandleDelayedChars()
        {
            lock(delayedChars)
            {
                if(!delayedChars.TryDequeue(out var delayed))
                {
                    delayedCharInProgress = false;
                    return;
                }

                delayedCharInProgress = true;

                var delay = TimeInterval.FromSeconds(delayed.Item1.TotalSeconds);
                machine.ScheduleAction(delay, _ =>
                {
                    CallCharReceived((byte)delayed.Item2);
                    HandleDelayedChars();
                });
            }
        }

        private TerminalTesterResult WaitForMatch(Func<TerminalTesterResult> resultMatcher, TimeInterval timeout, bool pauseEmulation = false)
        {
            var emulation = EmulationManager.Instance.CurrentEmulation;
            TerminalTesterResult immediateResult = null;

            lock(lines)
            {
                // Clear the old result and save the matcher for use in CharReceived
                this.testerResult = null;
                this.resultMatcher = resultMatcher;
                this.pauseEmulation = pauseEmulation;

                // Handle the case where the match has already happened
                immediateResult = MatchFailingStrings() ?? resultMatcher.Invoke();
                if(immediateResult != null)
                {
                    // Prevent matching in CharReceived in this case
                    this.resultMatcher = null;
                }
            }

            // Pause the emulation without the `lines` lock held to avoid deadlocks in some cases
            if(immediateResult != null)
            {
                if(pauseEmulation)
                {
                    this.Log(LogLevel.Warning, "Pause on match was requested, but the matching string had already " +
                        "been printed when the assertion was made. Pause time will not be deterministic.");
                    emulation.PauseAll();
                }
                return immediateResult;
            }
            // If we had timeout=0 and there was no immediate match, fail immediately
            else if(timeout == TimeInterval.Empty)
            {
                return null;
            }

            var timeoutEvent = machine.LocalTimeSource.EnqueueTimeoutEvent((ulong)timeout.TotalMilliseconds);
            var waitHandles = new [] { matchEvent, timeoutEvent.WaitHandle };

            var emulationPausedEvent = emulation.GetStartedStateChangedEvent(false);
            if(!emulation.IsStarted)
            {
                emulation.StartAll();
            }

            do
            {
                if(testerResult != null)
                {
                    // We know our machine is paused - we did that in WriteChar
                    // Now let's make sure the whole emulation is paused if necessary
                    if(pauseEmulation)
                    {
                        emulationPausedEvent.WaitOne();
                    }
                    return testerResult;
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

            lock(lines)
            {
                // Clear the saved matcher in the case of a timeout
                this.resultMatcher = null;
            }
            return null;
        }

        private TerminalTesterResult WaitForMultilineMatch(string[] patterns, TimeInterval? timeout = null, bool treatAsRegex = false, bool includeUnfinishedLine = false, bool pauseEmulation = false)
        {
            DebugHelper.Assert(patterns.Length > (includeUnfinishedLine ? 1 : 0));
            var eventName = "Lines starting with{1} >>{0}<<".FormatWith(patterns[0], treatAsRegex ? " regex" : string.Empty);

            var matcher = new MultilineMatcher(patterns, treatAsRegex, includeUnfinishedLine);
            var onCandidate = false;
            var lineIndexOffset = 0;

            var result = WaitForMatch(() =>
            {
                for(var i = lineIndexOffset; i < lines.Count; ++i)
                {
                    onCandidate = false;
                    if(matcher.FeedLine(lines[i]))
                    {
                        if(includeUnfinishedLine)
                        {
                            onCandidate = true;
                            break;
                        }
                        return HandleSuccess(eventName, i);
                    }
                    lineIndexOffset += 1;
                }

                if(onCandidate && includeUnfinishedLine && matcher.CheckUnfinishedLine(currentLineBuffer.ToString()))
                {
                    return HandleSuccess(eventName, CurrentLine);
                }

                return null;
            }, timeout ?? GlobalTimeout, pauseEmulation);

            if(result == null)
            {
                HandleFailure(eventName);
            }
            return result;
        }

        private TerminalTesterResult CheckFinishedLines(string pattern, bool regex, string eventName, bool matchFirstLine)
        {
            lock(lines)
            {
                string[] matchGroups = null;

                var matcher = !regex
                    ? (Predicate<Line>)(x => x.Content.Contains(pattern))
                    : (Predicate<Line>)(x =>
                {
                    var match = Regex.Match(x.Content, pattern);
                    if(match.Success)
                    {
                        matchGroups = GetMatchGroups(match);
                    }
                    return match.Success;
                });

                if(matchFirstLine)
                {
                    if(lines.Count == 0)
                    {
                        return null;
                    }
                    if(matcher(lines.First()))
                    {
                        return HandleSuccess(eventName, matchingLineId: 0);
                    }
                    return null;
                }

                var index = lines.FindIndex(matcher);
                if(index != -1)
                {
                    return HandleSuccess(eventName, matchingLineId: index, matchGroups: matchGroups);
                }

                return null;
            }
        }

        private TerminalTesterResult CheckUnfinishedLine(string pattern, bool regex, string eventName, bool matchAtStart = false)
        {
            var content = currentLineBuffer.ToString();

#if DEBUG_EVENTS
            this.Log(LogLevel.Noisy, "Current line buffer content: >>{0}<<", content);
#endif

            var matchStart = -1;
            var matchEnd = -1;
            string[] matchGroups = null;

            if(regex)
            {
                // In binary mode, make . match any character
                var options = binaryMode ? RegexOptions.Singleline : 0;
                var match = Regex.Match(content, pattern, options);
                if(match.Success)
                {
                    matchStart = match.Index;
                    matchEnd = match.Index + match.Length;
                    matchGroups = GetMatchGroups(match);
                }
            }
            else
            {
                // In binary mode, use ordinal (here: raw byte value) comparison.
                var comparisonType = binaryMode ? StringComparison.Ordinal : StringComparison.CurrentCulture;
                matchStart = content.IndexOf(pattern, comparisonType);
                if(matchStart != -1)
                {
                    matchEnd = matchStart + pattern.Length;
                }
            }

            // If we want a match at the start, then anything other than 0 simply won't cut it.
            if(matchAtStart && matchStart != 0)
            {
                return null;
            }

            // We know the match was successful, but only want to cut the current line buffer exactly when in binary mode.
            // Otherwise, we just throw out the whole thing.
            if(matchStart != -1)
            {
                return HandleSuccess(eventName, matchingLineId: CurrentLine, matchGroups: matchGroups,
                    matchStart: binaryMode ? matchStart : 0,
                    matchEnd: binaryMode ? (int?)matchEnd : null);
            }

            return null;
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
            if(binaryMode)
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

        private TerminalTesterResult HandleSuccess(string eventName, int matchingLineId, string[] matchGroups = null, int matchStart = 0, int? matchEnd = null)
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

                    content = currentLineBuffer.Unload(matchEnd).Substring(matchStart);
                }
                else if(numberOfLinesToCopy > 0)
                {
                    var item = lines[matchingLineId];
                    content = item.Content;
                    timestamp = item.VirtualTimestamp;
                }

                lines.RemoveRange(0, numberOfLinesToCopy);
                if(content != null && !binaryMode)
                {
                    content = content.StripNonSafeCharacters();
                }

                return new TerminalTesterResult(content, timestamp, matchGroups);
            }
        }

        private void HandleFailure(string eventName)
        {
            lock(lines)
            {
                ReportInner(eventName, "failure", lines.Count, true);
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
                // Don't say there was no newline if we are in binary mode as it does not operate on lines
                var newlineIndication = !binaryMode ? " [[no newline]]" : "";
                var displayString = currentLineBuffer.ToString();
                if(binaryMode)
                {
                    // Not using PrettyPrintCollection(Hex) to use simpler `01 02 03...` formatting.
                    displayString = string.Join(" ", displayString.Select(ch => ((byte)ch).ToHex()));
                }
                report.AppendFormat("{0}{1}\n", displayString, newlineIndication);
            }

            var virtMs = machine.ElapsedVirtualTime.TimeElapsed.TotalMilliseconds;
            report.AppendFormat("([host: {2}, virt: {3, 7}] {0} event: {1})\n", eventName, what, CustomDateTime.Now, virtMs);
        }

        private TerminalTesterResult MatchFailingStrings()
        {
            foreach(var failingString in failingStrings)
            {
                var lineMatch = CheckFinishedLines(failingString.pattern, failingString.treatAsRegex, "error", false);
                if(lineMatch != null)
                {
                    lineMatch.isFailingString = true;
                    return lineMatch;
                }
            }
            return null;
        }

        private IMachine machine;
        private SGRDecodingState sgrDecodingState;
        private string generatedReport;
        private Func<TerminalTesterResult> resultMatcher;
        private TerminalTesterResult testerResult;
        private bool pauseEmulation;
        private bool delayedCharInProgress;

        // Similarly how it is handled for FrameBufferTester it shouldn't matter if we unset the charEvent during deserialization
        // as we check for char match on load in `WaitForMatch` either way
        // Additionally in `IsIdle` the timeout would long since expire so it doesn't matter there either.
        [Constructor(false)]
        private AutoResetEvent charEvent;
        // The same logic as above also applies for the matchEvent.
        [Constructor(false)]
        private AutoResetEvent matchEvent;
        private readonly SafeStringBuilder currentLineBuffer;
        private readonly SafeStringBuilder sgrDecodingBuffer;
        private readonly EndLineOption endLineOption;
        private readonly bool binaryMode;
        private readonly List<Line> lines;
        private readonly SafeStringBuilder report;
        private readonly Queue<Tuple<TimeSpan, char>> delayedChars = new Queue<Tuple<TimeSpan, char>>();
        private readonly List<FailingString> failingStrings;

        private const char LineFeed = '\x0A';
        private const char CarriageReturn = '\x0D';
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

        private class MultilineMatcher
        {
            public MultilineMatcher(string[] patterns, bool regex, bool lastUnfinished)
            {
                if(regex)
                {
                    matchLine = patterns.Select(pattern =>
                    {
                        var matcher = new Regex(pattern);
                        return (Predicate<string>)(x => matcher.Match(x).Success);
                    }).ToArray();
                }
                else
                {
                    matchLine = patterns
                        .Select(pattern => (Predicate<string>)(x => x.Contains(pattern)))
                        .ToArray()
                    ;
                }

                length = patterns.Length - (lastUnfinished ? 2 : 1);
                candidates = new bool[length];
            }

            public bool FeedLine(Line line)
            {
                // Let lines[0] be the current `line` and lines[-n] be nth previous line.
                // The value of candidates[(last + n) % length] is a conjunction for k=1..n of line[-k] matching pattern[k - 1]
                if(candidates[last] && matchLine[length](line.Content))
                {
                    return true;
                }

                last = (last + 1) % length;
                var l = last;
                for(var i = 0; i < length; i++)
                {
                    var patternIndex = length - 1 - i;
                    if(candidates[l] || patternIndex == 0)
                    {
                        candidates[l] = matchLine[patternIndex](line.Content);
                    }
                    l = (l + 1) % length;
                }

                return false;
            }

            public bool CheckUnfinishedLine(string line)
            {
                return matchLine[matchLine.Length - 1](line);
            }

            private int last;
            private readonly Predicate<string>[] matchLine;
            private readonly bool[] candidates;
            private readonly int length;
        }

        private enum SGRDecodingState
        {
            NotDecoding,
            EscapeDetected,
            LeftBracketDetected
        }

        private struct FailingString
        {
            public string pattern;
            public bool treatAsRegex;

            public FailingString(string pattern, bool treatAsRegex)
            {
                this.pattern = pattern;
                this.treatAsRegex = treatAsRegex;
            }
        }
    }

    public class TerminalTesterResult
    {
        public TerminalTesterResult(string content, double timestamp, string[] groups = null, bool isFailingString = false)
        {
            this.line = content ?? string.Empty;
            this.timestamp = timestamp;
            this.groups = groups ?? new string[0];
            this.isFailingString = isFailingString;
        }

        public string line { get; }
        public double timestamp { get; }
        public string[] groups { get; set; }
        public bool isFailingString;
    }

    public enum EndLineOption
    {
        TreatLineFeedAsEndLine,
        TreatCarriageReturnAsEndLine
    }
}

