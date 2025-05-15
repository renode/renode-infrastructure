//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using AntShell.Commands;
using Antmicro.Renode.Time;
using Antmicro.Renode.UserInterface.Tokenizer;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.RESD;

namespace Antmicro.Renode.UserInterface.Commands
{
    public class ResdCommand : Command
    {
        public override void PrintHelp(ICommandInteraction writer)
        {
            base.PrintHelp(writer);
            writer.WriteLine();
            writer.WriteLine("You can use the following commands:");
            writer.WriteLine("'resd load NAME PATH'\tloads RESD file under identifier NAME");
            writer.WriteLine("'resd unload NAME'\tunloads RESD file with identifier NAME");
            writer.WriteLine("'resd list-blocks NAME'\tlist data blocks from RESD file with identifier NAME");
            writer.WriteLine("'resd describe-block NAME INDEX'\tshow informations about INDEXth block from RESD with identifier NAME");
            writer.WriteLine("'resd get-samples NAME INDEX \"START_TIME\" COUNT'\tlists COUNT samples starting at START_TIME from INDEXth block of RESD with identifier NAME");
            writer.WriteLine("'resd get-samples-range NAME INDEX \"START_TIME\" \"DURATION\"'\tlists DURATION samples starting at START_TIME from INDEXth block of RESD with identifier NAME");
            writer.WriteLine("'resd get-samples-range NAME INDEX \"START_TIME..END_TIME\"'\tlists samples between START_TIME and END_TIME from INDEXth block of RESD with identifier NAME");
            writer.WriteLine("'resd get-prop NAME INDEX PROP'\tread property PROP from INDEXth block of RESD with identifier NAME");
            writer.WriteLine($"  possible values for PROP are: {RESDPropertyNames}");
            writer.WriteLine();

            writer.WriteLine("Currently loaded RESD files:");
            foreach(var item in resdFiles)
            {
                var sourceName = item.Value.FilePath ?? "memory";
                writer.WriteLine($"{item.Key} @ {sourceName}");
            }
        }

        [Runnable]
        public void Run(ICommandInteraction writer, [Values("load")] LiteralToken action, LiteralToken internalName, StringToken filePath)
        {
            if(resdFiles.ContainsKey(internalName.Value))
            {
                writer.WriteError($"RESD file with identifier {internalName.Value} is already loaded");
                return;
            }

            try
            {
                resdFiles[internalName.Value] = new LowLevelRESDParser(filePath.Value);
            }
            catch(Exception e)
            {
                writer.WriteError($"Could not load RESD file: {e}");
                return;
            }
            writer.WriteLine($"RESD file from '{filePath.Value}' loaded under identifier '{internalName.Value}'");
        }

        [Runnable]
        public void Run(ICommandInteraction writer, [Values("unload", "list-blocks")] LiteralToken action, LiteralToken internalName)
        {
            if(!TryGetResdFile(writer, internalName.Value, out _))
            {
                return;
            }

            switch(action.Value)
            {
                case "unload":
                    Unload(writer, internalName.Value);
                    break;
                case "list-blocks":
                    ListBlocks(writer, internalName.Value);
                    break;
            }
        }

        [Runnable]
        public void Run(ICommandInteraction writer, [Values("get-prop")] LiteralToken action, LiteralToken internalName, DecimalIntegerToken index, LiteralToken property)
        {
            RESDProperty enumValue;
            if(!Enum.TryParse(property.Value, false, out enumValue))
            {
                writer.WriteError($"{property.Value} is not a valid property.");
                writer.WriteError($"Valid properties are: {RESDPropertyNames}");
                return;
            }
            DoForBlockWithIndex(writer, internalName.Value, index.Value, (block) =>
            {
                switch(enumValue)
                {
                    case RESDProperty.SampleType:
                        writer.WriteLine($"{block.SampleType}");
                        break;
                    case RESDProperty.ChannelID:
                        writer.WriteLine($"{block.ChannelId}");
                        break;
                    case RESDProperty.StartTime:
                        writer.WriteLine($"{TimeStampToTimeInterval(block.StartTime)}");
                        break;
                    case RESDProperty.EndTime:
                        writer.WriteLine($"{TimeStampToTimeInterval(block.GetEndTime())}");
                        break;
                    case RESDProperty.Duration:
                        writer.WriteLine($"{TimeStampToTimeInterval(block.Duration)}");
                        break;
                    default:
                        throw new Exception("unreachable");
                }
            });
        }

        [Runnable]
        public void Run(ICommandInteraction writer, [Values("get-samples")] LiteralToken action, LiteralToken internalName, DecimalIntegerToken index, StringToken startTimeString, DecimalIntegerToken count)
        {
            if(!TimeInterval.TryParse(startTimeString.Value, out var startTime))
            {
                writer.WriteError($"{startTimeString.Value} is invalid time-interval");
                return;
            }

            DoForBlockWithIndex(writer, internalName.Value, index.Value, (block) =>
            {
                foreach(var kv in block.Samples.SkipWhile(kv => kv.Key < startTime).Take((int)count.Value))
                {
                    writer.WriteLine($"{kv.Key}: {kv.Value}");
                }
            });
        }

        [Runnable]
        public void Run(ICommandInteraction writer, [Values("get-samples-range")] LiteralToken action, LiteralToken internalName, DecimalIntegerToken index, StringToken range)
        {
            var delimiterIndex = range.Value.IndexOf("..");
            if(delimiterIndex == -1)
            {
                writer.WriteError($"{range.Value} is invalid range");
                return;
            }

            var startTimeString = range.Value.Substring(0, delimiterIndex);
            var durationString = range.Value.Substring(delimiterIndex + 2, range.Value.Length - delimiterIndex - 2);
            if(String.IsNullOrEmpty(startTimeString) ||
                String.IsNullOrEmpty(durationString) ||
               !TimeInterval.TryParse(startTimeString, out var startTime) ||
               !TimeInterval.TryParse(durationString, out var duration))
            {
                writer.WriteError($"{range.Value} is invalid range");
                return;
            }

            DoForBlockWithIndex(writer, internalName.Value, index.Value, (block) =>
            {
                foreach(var kv in block.Samples.SkipWhile(kv => kv.Key < startTime).TakeWhile(kv => kv.Key <= startTime + duration))
                {
                    writer.WriteLine($"{kv.Key}: {kv.Value}");
                }
            });
        }

        [Runnable]
        public void Run(ICommandInteraction writer, [Values("get-samples-range")] LiteralToken action, LiteralToken internalName, DecimalIntegerToken index, StringToken startTimeString, StringToken endTimeString)
        {
            if(!TimeInterval.TryParse(startTimeString.Value, out var startTime))
            {
                writer.WriteError($"{startTimeString.Value} is invalid time-interval");
                return;
            }
            if(!TimeInterval.TryParse(endTimeString.Value, out var endTime))
            {
                writer.WriteError($"{endTimeString.Value} is invalid time-interval");
                return;
            }

            DoForBlockWithIndex(writer, internalName.Value, index.Value, (block) =>
            {
                foreach(var kv in block.Samples.SkipWhile(kv => kv.Key < startTime).TakeWhile(kv => kv.Key <= endTime))
                {
                    writer.WriteLine($"{kv.Key}: {kv.Value}");
                }
            });
        }

        [Runnable]
        public void Run(ICommandInteraction writer, [Values("describe-block")] LiteralToken action, LiteralToken internalName, DecimalIntegerToken index)
        {
            DoForBlockWithIndex(writer, internalName.Value, index.Value, (block) =>
            {
                writer.WriteLine($"Index: {index.Value}");
                writer.WriteLine($"Sample type: {block.SampleType}");
                writer.WriteLine($"Channel ID: {block.ChannelId}");
                writer.WriteLine($"Start Time: {TimeStampToTimeInterval(block.StartTime)}");
                writer.WriteLine($"End Time: {TimeStampToTimeInterval(block.GetEndTime())}");
                writer.WriteLine($"Duration: {TimeStampToTimeInterval(block.Duration)}");
                writer.WriteLine($"Samples count: {block.SamplesCount}");

                foreach(var kv in block.ExtraInformation)
                {
                    writer.WriteLine($"{kv.Key}: {kv.Value}");
                }

                if(block.Metadata.Count == 0)
                {
                    return;
                }

                writer.WriteLine($"Metadata: ");
                foreach(var item in block.Metadata)
                {
                    writer.WriteLine($"\t{item.Key} = {item.Value}");
                }
            });
        }

        public enum RESDProperty
        {
            SampleType,
            ChannelID,
            StartTime,
            EndTime,
            Duration,
            SamplesCount,
        }

        public ResdCommand(Monitor monitor)
            : base(monitor, "resd", "introspection for RESD files")
        {
            resdFiles = new Dictionary<string, LowLevelRESDParser>();
        }

        private void Unload(ICommandInteraction writer, string internalName)
        {
            resdFiles.Remove(internalName);
        }

        private void ListBlocks(ICommandInteraction writer, string internalName)
        {
            writer.WriteLine($"Blocks in {internalName}:");
            foreach(var item in resdFiles[internalName].GetDataBlockEnumerator().Select((block, idx) => new { idx, block }))
            {
                writer.WriteLine($"{item.idx + 1}. [{TimeStampToTimeInterval(item.block.StartTime)}..{TimeStampToTimeInterval(item.block.GetEndTime())}] {item.block.SampleType}:{item.block.ChannelId}");
            }
        }

        private bool TryGetResdFile(ICommandInteraction writer, string internalName, out LowLevelRESDParser resdFile)
        {
            if(!resdFiles.TryGetValue(internalName, out resdFile))
            {
                writer.WriteError($"RESD file with identifier {internalName} doesn't exist");
                return false;
            }
            return true;
        }

        private void DoForBlockWithIndex(ICommandInteraction writer, string internalName, long index, Action<IDataBlock> callback)
        {
            if(!TryGetResdFile(writer, internalName, out var resdFile))
            {
                return;
            }

            if(index <= 0)
            {
                writer.WriteError($"Index should be greater than 0");
                return;
            }

            var i = 0;
            foreach(var block in resdFile.GetDataBlockEnumerator())
            {
                if(++i == index)
                {
                    callback(block);
                    return;
                }
            }

            writer.WriteError($"Block with index {index} doesn't exist");
        }

        private TimeInterval TimeStampToTimeInterval(ulong timeStamp)
        {
            return TimeInterval.FromNanoseconds(timeStamp);
        }

        private string RESDPropertyNames => String.Join(", ", Enum.GetNames(typeof(RESDProperty)));

        private readonly IDictionary<string, LowLevelRESDParser> resdFiles;
    }
}

