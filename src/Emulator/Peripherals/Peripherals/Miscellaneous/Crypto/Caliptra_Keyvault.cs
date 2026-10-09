//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Crypto;

public class Caliptra_KeyVault : BasicDoubleWordPeripheral, IKnownSize
{
    public Caliptra_KeyVault(IMachine machine) : base(machine)
    {
        for(var i = 0; i < keySlots.Length; i++)
        {
            keySlots[i] = new KeyEntry(this, i);
        }
        DefineRegisters();
    }

    public override void Reset()
    {
        base.Reset();
        for(var i = 0; i < keySlots.Length; i++)
        {
            keySlots[i] = new KeyEntry(this, i);
        }
    }

    public bool TrySetKey(uint slot, byte[] key, DestinationCode destination = DestinationCode.Any, bool force = false)
    {
        if(slot >= NumberOfSlots)
        {
            this.ErrorLog("Trying to write to slot {0}, but there are at most {1} slots implemented", slot, NumberOfSlots);
            return false;
        }

        var success = keySlots[slot].WriteKey(key, destination, force);
        if(!success)
        {
            this.WarningLog("Failed to write to Key vault slot {0}", slot);
        }
        return success;
    }

    public bool TryGetKey(uint slot, out byte[] outKey, DestinationCode destination = DestinationCode.Any, bool force = false)
    {
        outKey = null;
        if(slot >= NumberOfSlots)
        {
            this.ErrorLog("Trying to read from slot {0}, but there are at most {1} slots implemented", slot, NumberOfSlots);
            return false;
        }

        if(!keySlots[slot].TryReadKey(out var key, destination, force))
        {
            this.WarningLog("Failed to read from Key vault slot {0}", slot);
            return false;
        }
        outKey = key.SelectMany(e => BitConverter.GetBytes(e)).ToArray();
        return true;
    }

    // Meant to be used interactively
    public byte[] GetKeyValue(uint slot)
    {
        TryGetKey(slot, out var key, force: true);
        return key;
    }

    public string[,] PrintKeyvaultContents(bool printSecret = false)
    {
        var table = new Table();
        table.AddRow("Slot", "LastDword", "WriteLocked", "UseLocked", "Allowed Destinations", "Secret");
        table.AddRows(keySlots,
            k => k.Idx.ToString(),
            k => k.LastDword.ToString(),
            k => k.WriteLocked.ToString(),
            k => k.UseLocked.ToString(),
            k => k.Destination.ToString(),
            k =>
            {
                if(!printSecret)
                {
                    // Prevents spamming the console if not requested explicitly
                    return "HIDDEN";
                }
                k.TryReadKey(out var key, force: true);
                return key.ToLazyHexString().ToString();
            }
        );

        return table.ToArray();
    }

    public long Size => 0xC04;

    private void DefineRegisters()
    {
        Registers.KeyControl.DefineMany(this, NumberOfSlots, (register, idx) =>
        {
            register
                // Both Write/Use Locks cannot be unset until device reset
                .WithFlag(0, writeCallback: (_, val) => keySlots[idx].WriteLocked |= val, valueProviderCallback: _ => keySlots[idx].WriteLocked, name: "lock_wr")
                .WithFlag(1, writeCallback: (_, val) => keySlots[idx].UseLocked |= val, valueProviderCallback: _ => keySlots[idx].UseLocked, name: "lock_use")
                .WithFlag(2, writeCallback: (_, val) => { if(val) keySlots[idx].Clear(); }, name: "clear")
                .WithReservedBits(3, 6)
                .WithEnumField<DoubleWordRegister, DestinationCode>(9, 9, FieldMode.Read, valueProviderCallback: _ => keySlots[idx].Destination, name: "dest_valid")
                .WithValueField(18, 4, FieldMode.Read, valueProviderCallback: _ => (ulong)keySlots[idx].LastDword, name: "last_dword")
                .WithReservedBits(22, 10);
        });

        Registers.KeyEntry.DefineMany(this, KeySize * NumberOfSlots, (register, idx) =>
        {
            // Key Entries are not readable or writeable by software
            register.WithReservedBits(0, 32);
        });

        Registers.ClearSecrets.Define(this)
            .WithFlag(0, out var debugValueSel, name: "sel_debug_value")
            .WithFlag(1, writeCallback: (_, val) =>
            {
                this.InfoLog("Seeding Keyvault with Debug Values");
                foreach(var kv in keySlots)
                {
                    // Bypasses normal lock logic
                    kv.WriteKey(Enumerable.Repeat(debugValueSel.Value ? DebugValues1 : DebugValues0, KeySize).ToArray(), DestinationCode.Any, true);
                }
            })
            .WithReservedBits(2, 30);
    }

    private readonly KeyEntry[] keySlots = new KeyEntry[NumberOfSlots];

    private const uint DebugValues0 = 0xAAAA_AAAA;
    private const uint DebugValues1 = 0x5555_5555;

    private const int NumberOfSlots = 24;
    private const int KeySize = 16;

    [Flags]
    public enum DestinationCode
    {
        None = 0,
        HmacKey = (1 << 0),
        HmacBlock = (1 << 1),
        MldsaSeed = (1 << 2),
        EccPKey = (1 << 3),
        EccSeed = (1 << 4),
        AesKey = (1 << 5),
        MlkemSeed = (1 << 6),
        MlkemMsg = (1 << 7),
        AxiDmaData = (1 << 8),
        Any = (1 << 9) - 1,
    }

    private struct KeyEntry
    {
        public KeyEntry(IPeripheral parent, int i)
        {
            this.parent = parent;
            Idx = i;
            entry = new uint[KeySize];
        }

        public bool WriteKey(byte[] newKey, DestinationCode destination = DestinationCode.Any, bool force = false)
        {
            return WriteKey(BitHelper.ToUInt32Array(newKey, true), destination, force);
        }

        public bool WriteKey(uint[] newKey, DestinationCode destination = DestinationCode.Any, bool force = false)
        {
            if(WriteLocked && !force)
            {
                parent.WarningLog("Key vault {0} has lock_write set; value not written", Idx);
                return false;
            }
            entry = newKey;
            LastDword = newKey.Length;
            Destination = destination;
            return true;
        }

        public bool TryReadKey(out uint[] key, DestinationCode destination = DestinationCode.Any, bool force = false)
        {
            if(UseLocked && !force)
            {
                key = new uint[KeySize];
                parent.WarningLog("Key vault {0} has lock_use set; value not read", Idx);
                return false;
            }
            if(!Destination.HasFlag(destination) && !force)
            {
                key = new uint[KeySize];
                parent.WarningLog("Key vault {0} has allowed writes mismatch; got {1}, but allowed are {2}", Idx, destination, Destination);
                return false;
            }
            key = this.entry;
            return true;
        }

        public void Clear()
        {
            if(WriteLocked || UseLocked)
            {
                parent.WarningLog("Key vault {0} cannot be cleared, since it was locked", Idx);
                return;
            }
            Array.Clear(entry);
            LastDword = 0;
        }

        public DestinationCode Destination { get; private set; }

        public int LastDword { get; private set; }

        public bool WriteLocked { get; set; }

        public bool UseLocked { get; set; }

        public int Idx { get; }

        private uint[] entry;
        private readonly IPeripheral parent;
    }

    private enum Registers
    {
        KeyControl = 0x0,
        KeyEntry = 0x600,
        ClearSecrets = 0xC00,
    }
}
