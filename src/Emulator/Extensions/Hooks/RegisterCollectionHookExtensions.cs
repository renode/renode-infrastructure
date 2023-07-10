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
        public static void AddBeforeReadByteHook(this IPeripheral @this, long offset, string script)
        {
            if(!(@this is IProvidesRegisterCollection<ByteRegisterCollection> registerColectionProvider))
            {
                throw new RecoverableException($"{@this.GetName()} is not a Byte peripheral");
            }

            var engine = new RegisterCollectionHookPythonEngine<byte>(@this, script);
            registerColectionProvider.AddBeforeReadHook<byte, ByteRegisterCollection>(offset, (addr) =>
            {
                engine.Hook(addr, null);
                return engine.Value;
            });
        }

        public static void AddAfterReadByteHook(this IPeripheral @this, long offset, string script)
        {
            if(!(@this is IProvidesRegisterCollection<ByteRegisterCollection> registerColectionProvider))
            {
                throw new RecoverableException($"{@this.GetName()} is not a Byte peripheral");
            }

            var engine = new RegisterCollectionHookPythonEngine<byte>(@this, script);
            registerColectionProvider.AddAfterReadHook<byte, ByteRegisterCollection>(offset, (addr, value) =>
            {
                engine.Hook(addr, value);
                return engine.Value;
            });
        }

        public static void AddBeforeWriteByteHook(this IPeripheral @this, long offset, string script)
        {
            if(!(@this is IProvidesRegisterCollection<ByteRegisterCollection> registerColectionProvider))
            {
                throw new RecoverableException($"{@this.GetName()} is not a Byte peripheral");
            }

            var engine = new RegisterCollectionHookPythonEngine<byte>(@this, script);
            registerColectionProvider.AddBeforeWriteHook<byte, ByteRegisterCollection>(offset, (addr, value) =>
            {
                engine.Hook(addr, value);
                return engine.Value;
            });
        }

        public static void AddAfterWriteByteHook(this IPeripheral @this, long offset, string script)
        {
            if(!(@this is IProvidesRegisterCollection<ByteRegisterCollection> registerColectionProvider))
            {
                throw new RecoverableException($"{@this.GetName()} is not a Byte peripheral");
            }

            var engine = new RegisterCollectionHookPythonEngine<byte>(@this, script);
            registerColectionProvider.AddAfterWriteHook<byte, ByteRegisterCollection>(offset, (addr, value) =>
            {
                engine.Hook(addr, value);
            });
        }

        public static void RemoveBeforeReadByteHook(this IPeripheral @this, long offset)
        {
            if(!(@this is IProvidesRegisterCollection<ByteRegisterCollection> registerColectionProvider))
            {
                throw new RecoverableException($"{@this.GetName()} is not a Byte peripheral");
            }
            registerColectionProvider.RemoveBeforeReadHook(offset);
        }

        public static void RemoveAfterReadByteHook(this IPeripheral @this, long offset)
        {
            if(!(@this is IProvidesRegisterCollection<ByteRegisterCollection> registerColectionProvider))
            {
                throw new RecoverableException($"{@this.GetName()} is not a Byte peripheral");
            }
            registerColectionProvider.RemoveAfterReadHook(offset);
        }

        public static void RemoveBeforeWriteByteHook(this IPeripheral @this, long offset)
        {
            if(!(@this is IProvidesRegisterCollection<ByteRegisterCollection> registerColectionProvider))
            {
                throw new RecoverableException($"{@this.GetName()} is not a Byte peripheral");
            }
            registerColectionProvider.RemoveBeforeWriteHook(offset);
        }

        public static void RemoveAfterWriteByteHook(this IPeripheral @this, long offset)
        {
            if(!(@this is IProvidesRegisterCollection<ByteRegisterCollection> registerColectionProvider))
            {
                throw new RecoverableException($"{@this.GetName()} is not a Byte peripheral");
            }
            registerColectionProvider.RemoveAfterWriteHook(offset);
        }

        public static void AddBeforeReadWordHook(this IPeripheral @this, long offset, string script)
        {
            if(!(@this is IProvidesRegisterCollection<WordRegisterCollection> registerColectionProvider))
            {
                throw new RecoverableException($"{@this.GetName()} is not a Word peripheral");
            }

            var engine = new RegisterCollectionHookPythonEngine<ushort>(@this, script);
            registerColectionProvider.AddBeforeReadHook<ushort, WordRegisterCollection>(offset, (addr) =>
            {
                engine.Hook(addr, null);
                return engine.Value;
            });
        }

        public static void AddAfterReadWordHook(this IPeripheral @this, long offset, string script)
        {
            if(!(@this is IProvidesRegisterCollection<WordRegisterCollection> registerColectionProvider))
            {
                throw new RecoverableException($"{@this.GetName()} is not a Word peripheral");
            }

            var engine = new RegisterCollectionHookPythonEngine<ushort>(@this, script);
            registerColectionProvider.AddAfterReadHook<ushort, WordRegisterCollection>(offset, (addr, value) =>
            {
                engine.Hook(addr, value);
                return engine.Value;
            });
        }

        public static void AddBeforeWriteWordHook(this IPeripheral @this, long offset, string script)
        {
            if(!(@this is IProvidesRegisterCollection<WordRegisterCollection> registerColectionProvider))
            {
                throw new RecoverableException($"{@this.GetName()} is not a Word peripheral");
            }

            var engine = new RegisterCollectionHookPythonEngine<ushort>(@this, script);
            registerColectionProvider.AddBeforeWriteHook<ushort, WordRegisterCollection>(offset, (addr, value) =>
            {
                engine.Hook(addr, value);
                return engine.Value;
            });
        }

        public static void AddAfterWriteWordHook(this IPeripheral @this, long offset, string script)
        {
            if(!(@this is IProvidesRegisterCollection<WordRegisterCollection> registerColectionProvider))
            {
                throw new RecoverableException($"{@this.GetName()} is not a Word peripheral");
            }

            var engine = new RegisterCollectionHookPythonEngine<ushort>(@this, script);
            registerColectionProvider.AddAfterWriteHook<ushort, WordRegisterCollection>(offset, (addr, value) =>
            {
                engine.Hook(addr, value);
            });
        }

        public static void RemoveBeforeReadWordHook(this IPeripheral @this, long offset)
        {
            if(!(@this is IProvidesRegisterCollection<WordRegisterCollection> registerColectionProvider))
            {
                throw new RecoverableException($"{@this.GetName()} is not a Word peripheral");
            }
            registerColectionProvider.RemoveBeforeReadHook(offset);
        }

        public static void RemoveAfterReadWordHook(this IPeripheral @this, long offset)
        {
            if(!(@this is IProvidesRegisterCollection<WordRegisterCollection> registerColectionProvider))
            {
                throw new RecoverableException($"{@this.GetName()} is not a Word peripheral");
            }
            registerColectionProvider.RemoveAfterReadHook(offset);
        }

        public static void RemoveBeforeWriteWordHook(this IPeripheral @this, long offset)
        {
            if(!(@this is IProvidesRegisterCollection<WordRegisterCollection> registerColectionProvider))
            {
                throw new RecoverableException($"{@this.GetName()} is not a Word peripheral");
            }
            registerColectionProvider.RemoveBeforeWriteHook(offset);
        }

        public static void RemoveAfterWriteWordHook(this IPeripheral @this, long offset)
        {
            if(!(@this is IProvidesRegisterCollection<WordRegisterCollection> registerColectionProvider))
            {
                throw new RecoverableException($"{@this.GetName()} is not a Word peripheral");
            }
            registerColectionProvider.RemoveAfterWriteHook(offset);
        }

        public static void AddBeforeReadDoubleWordHook(this IPeripheral @this, long offset, string script)
        {
            if(!(@this is IProvidesRegisterCollection<DoubleWordRegisterCollection> registerColectionProvider))
            {
                throw new RecoverableException($"{@this.GetName()} is not a DoubleWord peripheral");
            }

            var engine = new RegisterCollectionHookPythonEngine<uint>(@this, script);
            registerColectionProvider.AddBeforeReadHook<uint, DoubleWordRegisterCollection>(offset, (addr) =>
            {
                engine.Hook(addr, null);
                return engine.Value;
            });
        }

        public static void AddAfterReadDoubleWordHook(this IPeripheral @this, long offset, string script)
        {
            if(!(@this is IProvidesRegisterCollection<DoubleWordRegisterCollection> registerColectionProvider))
            {
                throw new RecoverableException($"{@this.GetName()} is not a DoubleWord peripheral");
            }

            var engine = new RegisterCollectionHookPythonEngine<uint>(@this, script);
            registerColectionProvider.AddAfterReadHook<uint, DoubleWordRegisterCollection>(offset, (addr, value) =>
            {
                engine.Hook(addr, value);
                return engine.Value;
            });
        }

        public static void AddBeforeWriteDoubleWordHook(this IPeripheral @this, long offset, string script)
        {
            if(!(@this is IProvidesRegisterCollection<DoubleWordRegisterCollection> registerColectionProvider))
            {
                throw new RecoverableException($"{@this.GetName()} is not a DoubleWord peripheral");
            }

            var engine = new RegisterCollectionHookPythonEngine<uint>(@this, script);
            registerColectionProvider.AddBeforeWriteHook<uint, DoubleWordRegisterCollection>(offset, (addr, value) =>
            {
                engine.Hook(addr, value);
                return engine.Value;
            });
        }

        public static void AddAfterWriteDoubleWordHook(this IPeripheral @this, long offset, string script)
        {
            if(!(@this is IProvidesRegisterCollection<DoubleWordRegisterCollection> registerColectionProvider))
            {
                throw new RecoverableException($"{@this.GetName()} is not a DoubleWord peripheral");
            }

            var engine = new RegisterCollectionHookPythonEngine<uint>(@this, script);
            registerColectionProvider.AddAfterWriteHook<uint, DoubleWordRegisterCollection>(offset, (addr, value) =>
            {
                engine.Hook(addr, value);
            });
        }

        public static void RemoveBeforeReadDoubleWordHook(this IPeripheral @this, long offset)
        {
            if(!(@this is IProvidesRegisterCollection<DoubleWordRegisterCollection> registerColectionProvider))
            {
                throw new RecoverableException($"{@this.GetName()} is not a DoubleWord peripheral");
            }
            registerColectionProvider.RemoveBeforeReadHook(offset);
        }

        public static void RemoveAfterReadDoubleWordHook(this IPeripheral @this, long offset)
        {
            if(!(@this is IProvidesRegisterCollection<DoubleWordRegisterCollection> registerColectionProvider))
            {
                throw new RecoverableException($"{@this.GetName()} is not a DoubleWord peripheral");
            }
            registerColectionProvider.RemoveAfterReadHook(offset);
        }

        public static void RemoveBeforeWriteDoubleWordHook(this IPeripheral @this, long offset)
        {
            if(!(@this is IProvidesRegisterCollection<DoubleWordRegisterCollection> registerColectionProvider))
            {
                throw new RecoverableException($"{@this.GetName()} is not a DoubleWord peripheral");
            }
            registerColectionProvider.RemoveBeforeWriteHook(offset);
        }

        public static void RemoveAfterWriteDoubleWordHook(this IPeripheral @this, long offset)
        {
            if(!(@this is IProvidesRegisterCollection<DoubleWordRegisterCollection> registerColectionProvider))
            {
                throw new RecoverableException($"{@this.GetName()} is not a DoubleWord peripheral");
            }
            registerColectionProvider.RemoveAfterWriteHook(offset);
        }

        public static void AddBeforeReadQuadWordHook(this IPeripheral @this, long offset, string script)
        {
            if(!(@this is IProvidesRegisterCollection<QuadWordRegisterCollection> registerColectionProvider))
            {
                throw new RecoverableException($"{@this.GetName()} is not a QuadWord peripheral");
            }

            var engine = new RegisterCollectionHookPythonEngine<ulong>(@this, script);
            registerColectionProvider.AddBeforeReadHook<ulong, QuadWordRegisterCollection>(offset, (addr) =>
            {
                engine.Hook(addr, null);
                return engine.Value;
            });
        }

        public static void AddAfterReadQuadWordHook(this IPeripheral @this, long offset, string script)
        {
            if(!(@this is IProvidesRegisterCollection<QuadWordRegisterCollection> registerColectionProvider))
            {
                throw new RecoverableException($"{@this.GetName()} is not a QuadWord peripheral");
            }

            var engine = new RegisterCollectionHookPythonEngine<ulong>(@this, script);
            registerColectionProvider.AddAfterReadHook<ulong, QuadWordRegisterCollection>(offset, (addr, value) =>
            {
                engine.Hook(addr, value);
                return engine.Value;
            });
        }

        public static void AddBeforeWriteQuadWordHook(this IPeripheral @this, long offset, string script)
        {
            if(!(@this is IProvidesRegisterCollection<QuadWordRegisterCollection> registerColectionProvider))
            {
                throw new RecoverableException($"{@this.GetName()} is not a QuadWord peripheral");
            }

            var engine = new RegisterCollectionHookPythonEngine<ulong>(@this, script);
            registerColectionProvider.AddBeforeWriteHook<ulong, QuadWordRegisterCollection>(offset, (addr, value) =>
            {
                engine.Hook(addr, value);
                return engine.Value;
            });
        }

        public static void AddAfterWriteQuadWordHook(this IPeripheral @this, long offset, string script)
        {
            if(!(@this is IProvidesRegisterCollection<QuadWordRegisterCollection> registerColectionProvider))
            {
                throw new RecoverableException($"{@this.GetName()} is not a QuadWord peripheral");
            }

            var engine = new RegisterCollectionHookPythonEngine<ulong>(@this, script);
            registerColectionProvider.AddAfterWriteHook<ulong, QuadWordRegisterCollection>(offset, (addr, value) =>
            {
                engine.Hook(addr, value);
            });
        }

        public static void RemoveBeforeReadQuadWordHook(this IPeripheral @this, long offset)
        {
            if(!(@this is IProvidesRegisterCollection<QuadWordRegisterCollection> registerColectionProvider))
            {
                throw new RecoverableException($"{@this.GetName()} is not a QuadWord peripheral");
            }
            registerColectionProvider.RemoveBeforeReadHook(offset);
        }

        public static void RemoveAfterReadQuadWordHook(this IPeripheral @this, long offset)
        {
            if(!(@this is IProvidesRegisterCollection<QuadWordRegisterCollection> registerColectionProvider))
            {
                throw new RecoverableException($"{@this.GetName()} is not a QuadWord peripheral");
            }
            registerColectionProvider.RemoveAfterReadHook(offset);
        }

        public static void RemoveBeforeWriteQuadWordHook(this IPeripheral @this, long offset)
        {
            if(!(@this is IProvidesRegisterCollection<QuadWordRegisterCollection> registerColectionProvider))
            {
                throw new RecoverableException($"{@this.GetName()} is not a QuadWord peripheral");
            }
            registerColectionProvider.RemoveBeforeWriteHook(offset);
        }

        public static void RemoveAfterWriteQuadWordHook(this IPeripheral @this, long offset)
        {
            if(!(@this is IProvidesRegisterCollection<QuadWordRegisterCollection> registerColectionProvider))
            {
                throw new RecoverableException($"{@this.GetName()} is not a QuadWord peripheral");
            }
            registerColectionProvider.RemoveAfterWriteHook(offset);
        }
    }
}
