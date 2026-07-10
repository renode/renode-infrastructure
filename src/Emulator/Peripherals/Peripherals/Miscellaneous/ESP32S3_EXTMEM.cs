//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2024 Sean "xobs" Cross
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class ESP32S3_EXTMEM : BasicDoubleWordPeripheral, IKnownSize
    {
        public ESP32S3_EXTMEM(IMachine machine) : base(machine)
        {
            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            iCacheEnabled = false;
            dCacheEnabled = false;
            iCacheFreeze = false;
            dCacheFreeze = false;
        }

        public long Size => 0x400;

        private void DefineRegisters()
        {
            // DCACHE_ENABLE and ICACHE_ENABLE use inverted logic: software writes 0 to enable.
            Registers.DCacheControl.Define(this)
                .WithFlag(0, valueProviderCallback: _ => dCacheEnabled, writeCallback: (_, val) => dCacheEnabled = !val, name: "DCACHE_ENABLE")
                .WithReservedBits(1, 1)
                .WithFlag(2, name: "DCACHE_SIZE_MODE")
                .WithValueField(3, 2, name: "DCACHE_BLOCKSIZE_MODE")
                .WithReservedBits(5, 27);

            Registers.DCacheControl1.Define(this, 0x3)
                .WithFlag(0, name: "DCACHE_SHUT_CORE0_BUS")
                .WithFlag(1, name: "DCACHE_SHUT_CORE1_BUS")
                .WithReservedBits(2, 30);

            Registers.DCacheSyncControl.Define(this, 0x1)
                .WithFlag(0, valueProviderCallback: _ => false, name: "DCACHE_INVALIDATE_ENA")
                .WithFlag(1, valueProviderCallback: _ => false, name: "DCACHE_WRITEBACK_ENA")
                .WithFlag(2, valueProviderCallback: _ => false, name: "DCACHE_CLEAN_ENA")
                .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => true, name: "DCACHE_SYNC_DONE")
                .WithReservedBits(4, 28);

            Registers.DCacheSyncAddress.Define(this)
                .WithValueField(0, 32, valueProviderCallback: _ => 0, name: "DCACHE_SYNC_ADDR");

            Registers.DCacheSyncSize.Define(this)
                .WithValueField(0, 23, valueProviderCallback: _ => 0, name: "DCACHE_SYNC_SIZE")
                .WithReservedBits(23, 9);

            Registers.DCachePreloadControl.Define(this, 0x2)
                .WithFlag(0, valueProviderCallback: _ => false, name: "DCACHE_PRELOAD_ENA")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => true, name: "DCACHE_PRELOAD_DONE")
                .WithFlag(2, name: "DCACHE_PRELOAD_ORDER")
                .WithReservedBits(3, 29);

            Registers.DCacheAutoloadControl.Define(this, 0x8)
                .WithFlag(0, name: "DCACHE_AUTOLOAD_SCT0_ENA")
                .WithFlag(1, name: "DCACHE_AUTOLOAD_SCT1_ENA")
                .WithFlag(2, name: "DCACHE_AUTOLOAD_ENA")
                .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => true, name: "DCACHE_AUTOLOAD_DONE")
                .WithFlag(4, valueProviderCallback: _ => false, name: "DCACHE_AUTOLOAD_ORDER")
                .WithValueField(5, 2, name: "DCACHE_AUTOLOAD_RQST")
                .WithValueField(7, 2, name: "DCACHE_AUTOLOAD_SIZE")
                .WithFlag(9, valueProviderCallback: _ => false, name: "DCACHE_AUTOLOAD_BUFFER_CLEAR")
                .WithReservedBits(10, 22);

            Registers.ICacheControl.Define(this)
                .WithFlag(0, valueProviderCallback: _ => iCacheEnabled, writeCallback: (_, val) => iCacheEnabled = !val, name: "ICACHE_ENABLE")
                .WithFlag(1, name: "ICACHE_WAY_MODE")
                .WithFlag(2, name: "ICACHE_SIZE_MODE")
                .WithFlag(3, name: "ICACHE_BLOCKSIZE_MODE")
                .WithReservedBits(4, 28);

            Registers.ICacheSyncControl.Define(this, 0x1)
                .WithFlag(0, name: "ICACHE_INVALIDATE_ENA")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => true, name: "ICACHE_SYNC_DONE")
                .WithReservedBits(2, 30);

            Registers.ICachePreloadControl.Define(this, 0x2)
                .WithFlag(0, name: "ICACHE_PRELOAD_ENA")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => true, name: "ICACHE_PRELOAD_DONE")
                .WithFlag(2, name: "ICACHE_PRELOAD_ORDER")
                .WithReservedBits(3, 29);

            Registers.ICacheAutoloadControl.Define(this, 0x8)
                .WithFlag(0, name: "ICACHE_AUTOLOAD_SCT0_ENA")
                .WithFlag(1, name: "ICACHE_AUTOLOAD_SCT1_ENA")
                .WithFlag(2, name: "ICACHE_AUTOLOAD_ENA")
                .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => true, name: "ICACHE_AUTOLOAD_DONE")
                .WithFlag(4, name: "ICACHE_AUTOLOAD_ORDER")
                .WithValueField(5, 2, name: "ICACHE_AUTOLOAD_RQST")
                .WithValueField(7, 2, name: "ICACHE_AUTOLOAD_SIZE")
                .WithFlag(9, name: "ICACHE_AUTOLOAD_BUFFER_CLEAR")
                .WithReservedBits(10, 22);

            Registers.CacheIlgIntClear.Define(this)
                .WithFlag(0, name: "ICACHE_SYNC_OP_FAULT_INT_CLR")
                .WithFlag(1, name: "ICACHE_PRELOAD_OP_FAULT_INT_CLR")
                .WithFlag(2, name: "DCACHE_SYNC_OP_FAULT_INT_CLR")
                .WithFlag(3, name: "DCACHE_PRELOAD_OP_FAULT_INT_CLR")
                .WithFlag(4, name: "DCACHE_WRITE_FLASH_INT_CLR")
                .WithFlag(5, name: "MMU_ENTRY_FAULT_INT_CLR")
                .WithFlag(6, name: "DCACHE_OCCUPY_EXC_INT_CLR")
                .WithFlag(7, name: "IBUS_CNT_OVF_INT_CLR")
                .WithFlag(8, name: "DBUS_CNT_OVF_INT_CLR")
                .WithReservedBits(9, 23);

            Registers.CacheWrapAroundControl.Define(this)
                .WithFlag(0, name: "CACHE_FLASH_WRAP_AROUND")
                .WithFlag(1, name: "CACHE_SRAM_RD_WRAP_AROUND")
                .WithReservedBits(2, 30);

            // Cache operations finish instantly, so both halves always read back as idle.
            Registers.CacheState.Define(this)
                .WithValueField(0, 12, FieldMode.Read, valueProviderCallback: _ => 1, name: "ICACHE_STATE")
                .WithValueField(12, 12, FieldMode.Read, valueProviderCallback: _ => 1, name: "DCACHE_STATE")
                .WithReservedBits(24, 8);

            Registers.CacheMmuOwner.Define(this)
                .WithValueField(0, 24, name: "CACHE_MMU_OWNER")
                .WithReservedBits(24, 8);

            Registers.DCacheFreeze.Define(this, 0x4)
                .WithFlag(0, valueProviderCallback: _ => dCacheFreeze, writeCallback: (_, val) => dCacheFreeze = val, name: "ENA")
                .WithFlag(1, name: "MODE")
                .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => dCacheFreeze, name: "DONE")
                .WithReservedBits(3, 29);

            Registers.ICacheFreeze.Define(this, 0x4)
                .WithFlag(0, valueProviderCallback: _ => iCacheFreeze, writeCallback: (_, val) => iCacheFreeze = val, name: "ENA")
                .WithFlag(1, name: "MODE")
                .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => iCacheFreeze, name: "DONE")
                .WithReservedBits(3, 29);
        }

        private bool iCacheEnabled;
        private bool dCacheEnabled;
        private bool iCacheFreeze;
        private bool dCacheFreeze;

        private enum Registers : long
        {
            DCacheControl                    = 0x000, // EXTMEM_DCACHE_CTRL
            DCacheControl1                   = 0x004, // EXTMEM_DCACHE_CTRL1
            DCacheTagPowerControl            = 0x008, // EXTMEM_DCACHE_TAG_POWER_CTRL
            DCachePrelockControl             = 0x00C, // EXTMEM_DCACHE_PRELOCK_CTRL
            DCachePrelockSct0Address         = 0x010, // EXTMEM_DCACHE_PRELOCK_SCT0_ADDR
            DCachePrelockSct1Address         = 0x014, // EXTMEM_DCACHE_PRELOCK_SCT1_ADDR
            DCachePrelockSctSize             = 0x018, // EXTMEM_DCACHE_PRELOCK_SCT_SIZE
            DCacheLockControl                = 0x01C, // EXTMEM_DCACHE_LOCK_CTRL
            DCacheLockAddress                = 0x020, // EXTMEM_DCACHE_LOCK_ADDR
            DCacheLockSize                   = 0x024, // EXTMEM_DCACHE_LOCK_SIZE
            DCacheSyncControl                = 0x028, // EXTMEM_DCACHE_SYNC_CTRL
            DCacheSyncAddress                = 0x02C, // EXTMEM_DCACHE_SYNC_ADDR
            DCacheSyncSize                   = 0x030, // EXTMEM_DCACHE_SYNC_SIZE
            DCacheOccupyControl              = 0x034, // EXTMEM_DCACHE_OCCUPY_CTRL
            DCacheOccupyAddress              = 0x038, // EXTMEM_DCACHE_OCCUPY_ADDR
            DCacheOccupySize                 = 0x03C, // EXTMEM_DCACHE_OCCUPY_SIZE
            DCachePreloadControl             = 0x040, // EXTMEM_DCACHE_PRELOAD_CTRL
            DCachePreloadAddress             = 0x044, // EXTMEM_DCACHE_PRELOAD_ADDR
            DCachePreloadSize                = 0x048, // EXTMEM_DCACHE_PRELOAD_SIZE
            DCacheAutoloadControl            = 0x04C, // EXTMEM_DCACHE_AUTOLOAD_CTRL
            DCacheAutoloadSct0Address        = 0x050, // EXTMEM_DCACHE_AUTOLOAD_SCT0_ADDR
            DCacheAutoloadSct0Size           = 0x054, // EXTMEM_DCACHE_AUTOLOAD_SCT0_SIZE
            DCacheAutoloadSct1Address        = 0x058, // EXTMEM_DCACHE_AUTOLOAD_SCT1_ADDR
            DCacheAutoloadSct1Size           = 0x05C, // EXTMEM_DCACHE_AUTOLOAD_SCT1_SIZE
            ICacheControl                    = 0x060, // EXTMEM_ICACHE_CTRL
            ICacheControl1                   = 0x064, // EXTMEM_ICACHE_CTRL1
            ICacheTagPowerControl            = 0x068, // EXTMEM_ICACHE_TAG_POWER_CTRL
            ICachePrelockControl             = 0x06C, // EXTMEM_ICACHE_PRELOCK_CTRL
            ICachePrelockSct0Address         = 0x070, // EXTMEM_ICACHE_PRELOCK_SCT0_ADDR
            ICachePrelockSct1Address         = 0x074, // EXTMEM_ICACHE_PRELOCK_SCT1_ADDR
            ICachePrelockSctSize             = 0x078, // EXTMEM_ICACHE_PRELOCK_SCT_SIZE
            ICacheLockControl                = 0x07C, // EXTMEM_ICACHE_LOCK_CTRL
            ICacheLockAddress                = 0x080, // EXTMEM_ICACHE_LOCK_ADDR
            ICacheLockSize                   = 0x084, // EXTMEM_ICACHE_LOCK_SIZE
            ICacheSyncControl                = 0x088, // EXTMEM_ICACHE_SYNC_CTRL
            ICacheSyncAddress                = 0x08C, // EXTMEM_ICACHE_SYNC_ADDR
            ICacheSyncSize                   = 0x090, // EXTMEM_ICACHE_SYNC_SIZE
            ICachePreloadControl             = 0x094, // EXTMEM_ICACHE_PRELOAD_CTRL
            ICachePreloadAddress             = 0x098, // EXTMEM_ICACHE_PRELOAD_ADDR
            ICachePreloadSize                = 0x09C, // EXTMEM_ICACHE_PRELOAD_SIZE
            ICacheAutoloadControl            = 0x0A0, // EXTMEM_ICACHE_AUTOLOAD_CTRL
            ICacheAutoloadSct0Address        = 0x0A4, // EXTMEM_ICACHE_AUTOLOAD_SCT0_ADDR
            ICacheAutoloadSct0Size           = 0x0A8, // EXTMEM_ICACHE_AUTOLOAD_SCT0_SIZE
            ICacheAutoloadSct1Address        = 0x0AC, // EXTMEM_ICACHE_AUTOLOAD_SCT1_ADDR
            ICacheAutoloadSct1Size           = 0x0B0, // EXTMEM_ICACHE_AUTOLOAD_SCT1_SIZE
            IBusToFlashStartVAddress         = 0x0B4, // EXTMEM_IBUS_TO_FLASH_START_VADDR
            IBusToFlashEndVAddress           = 0x0B8, // EXTMEM_IBUS_TO_FLASH_END_VADDR
            DBusToFlashStartVAddress         = 0x0BC, // EXTMEM_DBUS_TO_FLASH_START_VADDR
            DBusToFlashEndVAddress           = 0x0C0, // EXTMEM_DBUS_TO_FLASH_END_VADDR
            CacheAcsCntClear                 = 0x0C4, // EXTMEM_CACHE_ACS_CNT_CLR
            IBusAcsMissCnt                   = 0x0C8, // EXTMEM_IBUS_ACS_MISS_CNT
            IBusAcsCnt                       = 0x0CC, // EXTMEM_IBUS_ACS_CNT
            DBusAcsFlashMissCnt              = 0x0D0, // EXTMEM_DBUS_ACS_FLASH_MISS_CNT
            DBusAcsSpiramMissCnt             = 0x0D4, // EXTMEM_DBUS_ACS_SPIRAM_MISS_CNT
            DBusAcsCnt                       = 0x0D8, // EXTMEM_DBUS_ACS_CNT
            CacheIlgIntEnable                = 0x0DC, // EXTMEM_CACHE_ILG_INT_ENA
            CacheIlgIntClear                 = 0x0E0, // EXTMEM_CACHE_ILG_INT_CLR
            CacheIlgIntStatus                = 0x0E4, // EXTMEM_CACHE_ILG_INT_ST
            Core0AcsCacheIntEnable           = 0x0E8, // EXTMEM_CORE0_ACS_CACHE_INT_ENA
            Core0AcsCacheIntClear            = 0x0EC, // EXTMEM_CORE0_ACS_CACHE_INT_CLR
            Core0AcsCacheIntStatus           = 0x0F0, // EXTMEM_CORE0_ACS_CACHE_INT_ST
            Core1AcsCacheIntEnable           = 0x0F4, // EXTMEM_CORE1_ACS_CACHE_INT_ENA
            Core1AcsCacheIntClear            = 0x0F8, // EXTMEM_CORE1_ACS_CACHE_INT_CLR
            Core1AcsCacheIntStatus           = 0x0FC, // EXTMEM_CORE1_ACS_CACHE_INT_ST
            Core0DBusRejectStatus            = 0x100, // EXTMEM_CORE0_DBUS_REJECT_ST
            Core0DBusRejectVAddress          = 0x104, // EXTMEM_CORE0_DBUS_REJECT_VADDR
            Core0IBusRejectStatus            = 0x108, // EXTMEM_CORE0_IBUS_REJECT_ST
            Core0IBusRejectVAddress          = 0x10C, // EXTMEM_CORE0_IBUS_REJECT_VADDR
            Core1DBusRejectStatus            = 0x110, // EXTMEM_CORE1_DBUS_REJECT_ST
            Core1DBusRejectVAddress          = 0x114, // EXTMEM_CORE1_DBUS_REJECT_VADDR
            Core1IBusRejectStatus            = 0x118, // EXTMEM_CORE1_IBUS_REJECT_ST
            Core1IBusRejectVAddress          = 0x11C, // EXTMEM_CORE1_IBUS_REJECT_VADDR
            CacheMmuFaultContent             = 0x120, // EXTMEM_CACHE_MMU_FAULT_CONTENT
            CacheMmuFaultVAddress            = 0x124, // EXTMEM_CACHE_MMU_FAULT_VADDR
            CacheWrapAroundControl           = 0x128, // EXTMEM_CACHE_WRAP_AROUND_CTRL
            CacheMmuPowerControl             = 0x12C, // EXTMEM_CACHE_MMU_POWER_CTRL
            CacheState                       = 0x130, // EXTMEM_CACHE_STATE
            CacheEncryptDecryptRecordDisable = 0x134, // EXTMEM_CACHE_ENCRYPT_DECRYPT_RECORD_DISABLE
            CacheEncryptDecryptClkForceOn    = 0x138, // EXTMEM_CACHE_ENCRYPT_DECRYPT_CLK_FORCE_ON
            CacheBridgeArbiterControl        = 0x13C, // EXTMEM_CACHE_BRIDGE_ARBITER_CTRL
            CachePreloadIntControl           = 0x140, // EXTMEM_CACHE_PRELOAD_INT_CTRL
            CacheSyncIntControl              = 0x144, // EXTMEM_CACHE_SYNC_INT_CTRL
            CacheMmuOwner                    = 0x148, // EXTMEM_CACHE_MMU_OWNER
            CacheConfigMisc                  = 0x14C, // EXTMEM_CACHE_CONF_MISC
            DCacheFreeze                     = 0x150, // EXTMEM_DCACHE_FREEZE
            ICacheFreeze                     = 0x154, // EXTMEM_ICACHE_FREEZE
            ICacheAtomicOperateEnable        = 0x158, // EXTMEM_ICACHE_ATOMIC_OPERATE_ENA
            DCacheAtomicOperateEnable        = 0x15C, // EXTMEM_DCACHE_ATOMIC_OPERATE_ENA
            CacheRequest                     = 0x160, // EXTMEM_CACHE_REQUEST
            ClockGate                        = 0x164, // EXTMEM_CLOCK_GATE
            CacheTagObjectControl            = 0x180, // EXTMEM_CACHE_TAG_OBJECT_CTRL
            CacheTagWayObject                = 0x184, // EXTMEM_CACHE_TAG_WAY_OBJECT
            CacheVAddress                    = 0x188, // EXTMEM_CACHE_VADDR
            CacheTagContent                  = 0x18C, // EXTMEM_CACHE_TAG_CONTENT
            Date                             = 0x3FC, // EXTMEM_DATE
        }
    }
}
