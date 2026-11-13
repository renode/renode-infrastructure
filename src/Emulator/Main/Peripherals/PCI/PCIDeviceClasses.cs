//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Peripherals.PCI;

public enum PCISubClass
{
    // This enum is not comprehensive
    // When adding new device types to Renode, it should be expanded
    BridgeHost = 0x00,
    BridgeISA = 0x01,
    BridgeEISA = 0x02,
    BridgeMC = 0x03,
    BridgePCI = 0x04,
    BridgePCMCIA = 0x05,
    BridgeNuBus = 0x06,
    BridgeCardbus = 0x07,
    BridgeRaceway = 0x08,
    BridgeOther = 0x80,

    StorageSCSI = 0x00,
    StorageIDE = 0x01,
    StorageFloppy = 0x02,
    StorageIPI = 0x03,
    StorageRAID = 0x04,
    StorageSATA = 0x06,
    StorageSAS = 0x07,
    StorageOther = 0x80,
}

public enum PCIBaseClass
{
    NotDefined = 0x00,
    Storage = 0x01,
    Network = 0x02,
    Display = 0x03,
    Multimedia = 0x04,
    Memory = 0x05,
    Bridge = 0x06,
    Communication = 0x07,
    System = 0x08,
    Input = 0x09,
    Docking = 0x0A,
    Processor = 0x0B,
    Serial = 0x0C,
    Wireless = 0x0D,
    Intelligent = 0x0E,
    Satellite = 0x0F,
    Crypto = 0x10,
    Signalprocessing = 0x11,
    Accelerator = 0x12,
    Unclassified = 0xFF,
}
