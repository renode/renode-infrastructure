//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
// 
using System;
using System.Linq;
using System.Globalization;
using System.Threading;
using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Peripherals.CPU
{
    public static class ExecutionTracerExtensions
    {
        public static void EnableExecutionTracing(this TranslationCPU @this, string file, ExecutionTracer.Format format)
        {
            var tracer = new ExecutionTracer(format, file);
            EmulationManager.Instance.EmulationChanged += () =>
            {
                tracer.Dispose();
            };
            
            tracer.AttachTo(@this);
        }
    }
    
    public class ExecutionTracer : IDisposable
    {
        public ExecutionTracer(Format format, string file)
        {
            blocks = new BlockingCollection<Block>();
            this.file = file;
            this.format = format;
        }

        public void Dispose()
        {
            if(underlyingThread != null)
            {
                cts.Cancel();
                underlyingThread.Join();
                underlyingThread = null;
            }
        }

        public void AttachTo(TranslationCPU cpu)
        {
            attachedCPU = cpu;
            cpu.SetHookAtBlockEnd(HandleBlock);
            Start();
        }

        private void Start()
        {
            cts = new CancellationTokenSource();
            
            underlyingThread = new Thread(WriterThreadBody);
            underlyingThread.IsBackground = true;
            underlyingThread.Name = "Execution tracer worker";
            underlyingThread.Start();
        }

        private void WriterThreadBody()
        {
            // truncate the file
            File.WriteAllText(file, string.Empty);
            
            while(true)
            {
                try
                {
                    var val = string.Empty;
                    var block = blocks.Take(cts.Token);

                    var pc = block.StartingPc;
                    var counter = 0;

                    while(counter < (int)block.Size)
                    {
                        var mem = attachedCPU.GetMachine().SystemBus.ReadBytes(pc, MaxOpcodeBytes);

                        // TODO: what about flags?
                        if(!attachedCPU.Disassembler.TryDisassembleInstruction(pc, mem, 0, out var result))
                        {
                            val += $"Couldn't disassemble opcode at PC 0x{pc:X}\n";
                            break;
                        }
                        else
                        {
                            switch(format)
                            {
                                case Format.PC:
                                    val += $"0x{pc:X}\n"; 
                                    break;

                                case Format.Opcode:
                                    val += "0x" + result.OpcodeString.ToUpper() + "\n";
                                    break;

                                default:
                                    attachedCPU.Log(LogLevel.Error, "Unsupported format: {0}", format);
                                    break;
                            }
                            
                            pc += (ulong)result.OpcodeSize;
                            counter++;
                        }
                    }
                    
                    // this opens/closes the file for each PC
                    // * it ensures flushing
                    // * it might not be optimal
                    File.AppendAllText(file, val);
                }
                catch(OperationCanceledException)
                {
                    break;
                }
            }
        }

        private void HandleBlock(ulong pc, uint instructionsInBlock)
        {
            if(instructionsInBlock == 0)
            {
                // ignore
                return;
            }

            Logger.Log(LogLevel.Error, "Handling block starting at 0x{0:X} of size {1} instr", pc, instructionsInBlock);
            blocks.Add(new Block { StartingPc = pc, Size = instructionsInBlock });
        }
        
        private TranslationCPU attachedCPU;
        private Thread underlyingThread;
        private CancellationTokenSource cts;

        private readonly BlockingCollection<Block> blocks;
        private readonly string file;
        private readonly Format format;

        private const int MaxOpcodeBytes = 16;
        
        public enum Format
        {
            PC,
            Opcode
        }

        private struct Block
        {
            public ulong StartingPc;
            public ulong Size;

            public override string ToString()
            {
                return $"[Block: 0x{StartingPc:X} of size {Size} instructions]";
            }
        }
    }
}
