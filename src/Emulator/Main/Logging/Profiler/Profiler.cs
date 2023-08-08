//
// Copyright (c) 2010-2022 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Logging.Profiling
{
    public class Profiler : IDisposable
    {
        public Profiler(IMachine machine, string outputPath)
        {
            this.machine = machine;

            try
            {
                output = new FileStream(outputPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Delete);
            }
            catch(IOException ex)
            {
                throw new RecoverableException(ex);
            }

            EnableProfiling();
            machine.PeripheralsChanged += OnPeripheralsChanged;
        }

        public void Log(BaseEntry entry)
        {
            lock(locker)
            {
                if(!headerWritten)
                {
                    WriteHeader();
                }
                var bytes = Serialize(entry);
                output.Write(bytes, 0, bytes.Length);
            }
        }

        public void Dispose()
        {
            output.Flush();
            output.Dispose();
        }

        private void EnableProfiling()
        {
            var cpus = machine.GetPeripheralsOfType<ICPUWithMetrics>();
            foreach(var cpu in cpus)
            {
                cpu.EnableProfiling();
            }
        }

        private void OnPeripheralsChanged(IMachine machine, PeripheralsChangedEventArgs args)
        {
            if(args.Operation != PeripheralsChangedEventArgs.PeripheralChangeType.Addition)
            {
                return;
            }

            var cpu = args.Peripheral as ICPUWithMetrics;
            if(cpu != null)
            {
                cpu.EnableProfiling();
            }
        }

        private void WriteHeader()
        {
            headerWritten = true;
            var header = new ProfilerHeader();
            header.RegisterPeripherals(machine);
            output.Write(header.Bytes, 0, header.Bytes.Length);
        }

        private byte[] Serialize(object target)
        {
            var size = Marshal.SizeOf(target);
            var result = new byte[size];
            var handler = default(GCHandle);

            try
            {
                handler = GCHandle.Alloc(result, GCHandleType.Pinned);
                Marshal.StructureToPtr(target, handler.AddrOfPinnedObject(), false);
            }
            finally
            {
                if(handler.IsAllocated)
                {
                    handler.Free();
                }
            }

            return result;
        }

        private bool headerWritten;

        private readonly static object locker = new object();
        private readonly FileStream output;
        private readonly IMachine machine;
    }
}
