/********************************************************
*
* Warning!
* This file was generated automatically.
* Please do not edit. Changes should be made in the
* appropriate *.tt file.
*
*/

using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals;

namespace Antmicro.Renode.Hooks
{
    public static class RegisterCollectionHookExtensions
    {
        public static void AddBeforeReadByteHook(this IProvidesRegisterCollection<ByteRegisterCollection> @this, long offset, string script)
        {
            var engine = new RegisterCollectionHookPythonEngine<byte, ByteRegisterCollection>(@this, script);
            @this.AddBeforeReadHook<byte, ByteRegisterCollection>(offset, (addr) =>
            {
                engine.Hook(addr, null);
                return engine.Value;
            });
        }

        public static void AddAfterReadByteHook(this IProvidesRegisterCollection<ByteRegisterCollection> @this, long offset, string script)
        {
            var engine = new RegisterCollectionHookPythonEngine<byte, ByteRegisterCollection>(@this, script);
            @this.AddAfterReadHook<byte, ByteRegisterCollection>(offset, (addr, value) =>
            {
                engine.Hook(addr, value);
                return engine.Value;
            });
        }

        public static void AddBeforeWriteByteHook(this IProvidesRegisterCollection<ByteRegisterCollection> @this, long offset, string script)
        {
            var engine = new RegisterCollectionHookPythonEngine<byte, ByteRegisterCollection>(@this, script);
            @this.AddBeforeWriteHook<byte, ByteRegisterCollection>(offset, (addr, value) =>
            {
                engine.Hook(addr, value);
                return engine.Value;
            });
        }

        public static void AddAfterWriteByteHook(this IProvidesRegisterCollection<ByteRegisterCollection> @this, long offset, string script)
        {

            var engine = new RegisterCollectionHookPythonEngine<byte, ByteRegisterCollection>(@this, script);
            @this.AddAfterWriteHook<byte, ByteRegisterCollection>(offset, (addr, value) =>
            {
                engine.Hook(addr, value);
            });
        }

        public static void RemoveBeforeReadByteHook(this IProvidesRegisterCollection<ByteRegisterCollection> @this, long offset)
        {
            @this.RemoveBeforeReadHook(offset);
        }

        public static void RemoveAfterReadByteHook(this IProvidesRegisterCollection<ByteRegisterCollection> @this, long offset)
        {
            @this.RemoveAfterReadHook(offset);
        }

        public static void RemoveBeforeWriteByteHook(this IProvidesRegisterCollection<ByteRegisterCollection> @this, long offset)
        {
            @this.RemoveBeforeWriteHook(offset);
        }

        public static void RemoveAfterWriteByteHook(this IProvidesRegisterCollection<ByteRegisterCollection> @this, long offset)
        {
            @this.RemoveAfterWriteHook(offset);
        }

        public static void AddBeforeReadWordHook(this IProvidesRegisterCollection<WordRegisterCollection> @this, long offset, string script)
        {
            var engine = new RegisterCollectionHookPythonEngine<ushort, WordRegisterCollection>(@this, script);
            @this.AddBeforeReadHook<ushort, WordRegisterCollection>(offset, (addr) =>
            {
                engine.Hook(addr, null);
                return engine.Value;
            });
        }

        public static void AddAfterReadWordHook(this IProvidesRegisterCollection<WordRegisterCollection> @this, long offset, string script)
        {
            var engine = new RegisterCollectionHookPythonEngine<ushort, WordRegisterCollection>(@this, script);
            @this.AddAfterReadHook<ushort, WordRegisterCollection>(offset, (addr, value) =>
            {
                engine.Hook(addr, value);
                return engine.Value;
            });
        }

        public static void AddBeforeWriteWordHook(this IProvidesRegisterCollection<WordRegisterCollection> @this, long offset, string script)
        {
            var engine = new RegisterCollectionHookPythonEngine<ushort, WordRegisterCollection>(@this, script);
            @this.AddBeforeWriteHook<ushort, WordRegisterCollection>(offset, (addr, value) =>
            {
                engine.Hook(addr, value);
                return engine.Value;
            });
        }

        public static void AddAfterWriteWordHook(this IProvidesRegisterCollection<WordRegisterCollection> @this, long offset, string script)
        {

            var engine = new RegisterCollectionHookPythonEngine<ushort, WordRegisterCollection>(@this, script);
            @this.AddAfterWriteHook<ushort, WordRegisterCollection>(offset, (addr, value) =>
            {
                engine.Hook(addr, value);
            });
        }

        public static void RemoveBeforeReadWordHook(this IProvidesRegisterCollection<WordRegisterCollection> @this, long offset)
        {
            @this.RemoveBeforeReadHook(offset);
        }

        public static void RemoveAfterReadWordHook(this IProvidesRegisterCollection<WordRegisterCollection> @this, long offset)
        {
            @this.RemoveAfterReadHook(offset);
        }

        public static void RemoveBeforeWriteWordHook(this IProvidesRegisterCollection<WordRegisterCollection> @this, long offset)
        {
            @this.RemoveBeforeWriteHook(offset);
        }

        public static void RemoveAfterWriteWordHook(this IProvidesRegisterCollection<WordRegisterCollection> @this, long offset)
        {
            @this.RemoveAfterWriteHook(offset);
        }

        public static void AddBeforeReadDoubleWordHook(this IProvidesRegisterCollection<DoubleWordRegisterCollection> @this, long offset, string script)
        {
            var engine = new RegisterCollectionHookPythonEngine<uint, DoubleWordRegisterCollection>(@this, script);
            @this.AddBeforeReadHook<uint, DoubleWordRegisterCollection>(offset, (addr) =>
            {
                engine.Hook(addr, null);
                return engine.Value;
            });
        }

        public static void AddAfterReadDoubleWordHook(this IProvidesRegisterCollection<DoubleWordRegisterCollection> @this, long offset, string script)
        {
            var engine = new RegisterCollectionHookPythonEngine<uint, DoubleWordRegisterCollection>(@this, script);
            @this.AddAfterReadHook<uint, DoubleWordRegisterCollection>(offset, (addr, value) =>
            {
                engine.Hook(addr, value);
                return engine.Value;
            });
        }

        public static void AddBeforeWriteDoubleWordHook(this IProvidesRegisterCollection<DoubleWordRegisterCollection> @this, long offset, string script)
        {
            var engine = new RegisterCollectionHookPythonEngine<uint, DoubleWordRegisterCollection>(@this, script);
            @this.AddBeforeWriteHook<uint, DoubleWordRegisterCollection>(offset, (addr, value) =>
            {
                engine.Hook(addr, value);
                return engine.Value;
            });
        }

        public static void AddAfterWriteDoubleWordHook(this IProvidesRegisterCollection<DoubleWordRegisterCollection> @this, long offset, string script)
        {

            var engine = new RegisterCollectionHookPythonEngine<uint, DoubleWordRegisterCollection>(@this, script);
            @this.AddAfterWriteHook<uint, DoubleWordRegisterCollection>(offset, (addr, value) =>
            {
                engine.Hook(addr, value);
            });
        }

        public static void RemoveBeforeReadDoubleWordHook(this IProvidesRegisterCollection<DoubleWordRegisterCollection> @this, long offset)
        {
            @this.RemoveBeforeReadHook(offset);
        }

        public static void RemoveAfterReadDoubleWordHook(this IProvidesRegisterCollection<DoubleWordRegisterCollection> @this, long offset)
        {
            @this.RemoveAfterReadHook(offset);
        }

        public static void RemoveBeforeWriteDoubleWordHook(this IProvidesRegisterCollection<DoubleWordRegisterCollection> @this, long offset)
        {
            @this.RemoveBeforeWriteHook(offset);
        }

        public static void RemoveAfterWriteDoubleWordHook(this IProvidesRegisterCollection<DoubleWordRegisterCollection> @this, long offset)
        {
            @this.RemoveAfterWriteHook(offset);
        }

        public static void AddBeforeReadQuadWordHook(this IProvidesRegisterCollection<QuadWordRegisterCollection> @this, long offset, string script)
        {
            var engine = new RegisterCollectionHookPythonEngine<ulong, QuadWordRegisterCollection>(@this, script);
            @this.AddBeforeReadHook<ulong, QuadWordRegisterCollection>(offset, (addr) =>
            {
                engine.Hook(addr, null);
                return engine.Value;
            });
        }

        public static void AddAfterReadQuadWordHook(this IProvidesRegisterCollection<QuadWordRegisterCollection> @this, long offset, string script)
        {
            var engine = new RegisterCollectionHookPythonEngine<ulong, QuadWordRegisterCollection>(@this, script);
            @this.AddAfterReadHook<ulong, QuadWordRegisterCollection>(offset, (addr, value) =>
            {
                engine.Hook(addr, value);
                return engine.Value;
            });
        }

        public static void AddBeforeWriteQuadWordHook(this IProvidesRegisterCollection<QuadWordRegisterCollection> @this, long offset, string script)
        {
            var engine = new RegisterCollectionHookPythonEngine<ulong, QuadWordRegisterCollection>(@this, script);
            @this.AddBeforeWriteHook<ulong, QuadWordRegisterCollection>(offset, (addr, value) =>
            {
                engine.Hook(addr, value);
                return engine.Value;
            });
        }

        public static void AddAfterWriteQuadWordHook(this IProvidesRegisterCollection<QuadWordRegisterCollection> @this, long offset, string script)
        {

            var engine = new RegisterCollectionHookPythonEngine<ulong, QuadWordRegisterCollection>(@this, script);
            @this.AddAfterWriteHook<ulong, QuadWordRegisterCollection>(offset, (addr, value) =>
            {
                engine.Hook(addr, value);
            });
        }

        public static void RemoveBeforeReadQuadWordHook(this IProvidesRegisterCollection<QuadWordRegisterCollection> @this, long offset)
        {
            @this.RemoveBeforeReadHook(offset);
        }

        public static void RemoveAfterReadQuadWordHook(this IProvidesRegisterCollection<QuadWordRegisterCollection> @this, long offset)
        {
            @this.RemoveAfterReadHook(offset);
        }

        public static void RemoveBeforeWriteQuadWordHook(this IProvidesRegisterCollection<QuadWordRegisterCollection> @this, long offset)
        {
            @this.RemoveBeforeWriteHook(offset);
        }

        public static void RemoveAfterWriteQuadWordHook(this IProvidesRegisterCollection<QuadWordRegisterCollection> @this, long offset)
        {
            @this.RemoveAfterWriteHook(offset);
        }

    }
}
