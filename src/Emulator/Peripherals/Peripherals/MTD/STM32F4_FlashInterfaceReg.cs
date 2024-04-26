using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Memory;
using System.Reflection;

namespace Antmicro.Renode.Peripherals.MTD {

    public class STM32F4_FlashInterfaceReg : IDoubleWordPeripheral, IExternal {

        private partial class Sector {

            public uint Offset { get; private set; }
            public uint Size { get; private set; }

            public Sector(uint offset, uint size) {
                Offset = offset;
                Size = size;
            }

        }

        private enum KeyState {
            KEY_1,
            KEY_2
        };

        private enum _registers : long {
            FLASH_KEYR = 0x4,
            FLASH_SR = 0x0C,
            FLASH_CR = 0x10
        };

        private Dictionary<uint, Sector> _sectors = new Dictionary<uint, Sector> {
            {  0, new Sector (offset: 0x0000000, size: 0x04000) },
            {  1, new Sector (offset: 0x0004000, size: 0x04000) },
            {  2, new Sector (offset: 0x0008000, size: 0x04000) },
            {  3, new Sector (offset: 0x000C000, size: 0x04000) },
            {  4, new Sector (offset: 0x0010000, size: 0x10000) },
            {  5, new Sector (offset: 0x0020000, size: 0x20000) },
            {  6, new Sector (offset: 0x0040000, size: 0x20000) },
            {  7, new Sector (offset: 0x0060000, size: 0x20000) },
            {  8, new Sector (offset: 0x0080000, size: 0x20000) },
            {  9, new Sector (offset: 0x00A0000, size: 0x20000) },
            { 10, new Sector (offset: 0x00C0000, size: 0x20000) },
            { 11, new Sector (offset: 0x00E0000, size: 0x20000) }
        };

        private readonly DoubleWordRegisterCollection _registers_coll;
        private uint _sectorNumber;
        private KeyState _state = KeyState.KEY_1;
        private MappedMemory _flashMemory;
        private bool _busy = false;

        public STM32F4_FlashInterfaceReg(MappedMemory flashMemory) {
            var registersMap = new Dictionary<long, DoubleWordRegister> {
                {(long)_registers.FLASH_KEYR, new DoubleWordRegister(this, 0x0)
                    .WithValueField(0, 32, name: "KEY", mode: FieldMode.Read | FieldMode.Write, changeCallback: (_, value) => {

                            var unlocked = false;
                            switch (_state) {
                                case KeyState.KEY_1:
                                    if (value == 0x45670123) {
                                        _state = KeyState.KEY_2;
                                    }
                                    break;

                                case KeyState.KEY_2:
                                    _state = KeyState.KEY_1;
                                    if (value == 0xCDEF89AB) {
                                        unlocked = true;
                                    }
                                    break;

                                default:
                                    _state = KeyState.KEY_1;
                                    break;
                            }

                            this.Log(LogLevel.Debug, $"KEY: {value}, unlocked? { ( unlocked ? "yes" : "no" ) }");
                            if (unlocked) {
                                var valCR = _registers_coll.Read((long)_registers.FLASH_CR);
                                valCR &= ~(uint)(1 << 30) << 1;
                                _registers_coll.Write((long)_registers.FLASH_CR, valCR);
                            }
                        })
                },
                {(long)_registers.FLASH_SR, new DoubleWordRegister(this, 0x0)
                    .WithFlag(16, name: "BSY", mode: FieldMode.Read, valueProviderCallback: _ => _busy)
                    .WithReservedBits(17, 15)
                },
                {(long)_registers.FLASH_CR, new DoubleWordRegister(this, 0x80000000)
                    .WithFlag(0, name: "PG", mode: FieldMode.Write | FieldMode.Read)
                    .WithFlag(1, name: "SER", mode: FieldMode.Write | FieldMode.Read)
                    .WithValueField(3, 4, name: "SNB", changeCallback: (_ ,value) => { 
                            _sectorNumber = value; 
                        })
                    .WithTag("PSIZE", 8, 2)
                    .WithFlag(16, valueProviderCallback: _ => false, name: "STRT", changeCallback: (_, value) => {

                            var valCR = _registers_coll.Read((long)_registers.FLASH_CR);
                            _busy = value;

                            if (value && (valCR & (uint)(1 << 1)) != 0) {
                                var sector = _sectors[_sectorNumber];
                                this.Log(LogLevel.Debug, $"Started erasing sector #{_sectorNumber} (offset {sector.Offset}, size {sector.Size}) ");

                                var pattern = Enumerable.Repeat((byte)0xFF, 1).ToArray();
                                for (int i = 0; i < sector.Size; i++) {
                                    _flashMemory.WriteByte(sector.Offset + i, 0xff);
                                }

                                _registers_coll.Write((long)_registers.FLASH_CR, valCR & ~(uint)(1 << 16));
                            }

                            _busy = false;
                        })
                    .WithFlag(24, name: "EOPIE", mode: FieldMode.Write | FieldMode.Read)
                    .WithFlag(25, name: "ERRIE", mode: FieldMode.Write | FieldMode.Read)
                    .WithFlag(31, name: "LOCK", mode: FieldMode.Write | FieldMode.Read)
                },
            };

            this._registers_coll = new DoubleWordRegisterCollection(this, registersMap);
            this._flashMemory = flashMemory;
        }

        public uint ReadDoubleWord(long offset) {
            return this._registers_coll.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value) {

            if (offset == (long)_registers.FLASH_CR) {
                var valCR = _registers_coll.Read((long)_registers.FLASH_CR);
                if ((valCR >> 31) == 1) {
                    return;
                }
            }

            this._registers_coll.Write(offset, value);
        }
		public void Reset() {
            _registers_coll.Reset();
            _state = KeyState.KEY_1;
        }
        

    }

}