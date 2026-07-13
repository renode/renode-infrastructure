//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Peripherals.SD;
using Antmicro.Renode.Utilities;

using NUnit.Framework;

namespace Antmicro.Renode.PeripheralsTests
{
    [TestFixture]
    public class SDCardEmmcTest
    {
        [Test]
        public void ShouldRejectSpiMode()
        {
            Assert.Throws<Antmicro.Renode.Exceptions.ConstructionException>(() => new SDCard(0x1000000, spiMode: true, emmc: true));
        }

        [Test]
        public void ShouldKeepHardwarePartitionsSeparate()
        {
            const long capacity = 0x1000000; // 16 MiB
            const uint sector = 0x1000 / 512;
            var uda = new byte[] { 0x55, 0x44, 0x41, 0x30 };   // "UDA0"
            var boot = new byte[] { 0x42, 0x4F, 0x4F, 0x54 };  // "BOOT"

            using(var card = new SDCard(capacity, emmc: true))
            {
                card.Reset();

                WriteAt(card, sector, uda); // PARTITION_ACCESS defaults to 0 (User Data Area)
                SwitchPartition(card, 1); // -> Boot Area 1
                WriteAt(card, sector, boot);

                SwitchPartition(card, 0);
                SwitchPartition(card, 1);
                CollectionAssert.AreEqual(boot, ReadAt(card, sector, boot.Length), "boot area should retain its bytes after switching away and back");

                SwitchPartition(card, 0);
                CollectionAssert.AreEqual(uda, ReadAt(card, sector, uda.Length), "the UDA must be untouched by the boot-area write");
            }
        }

        [Test]
        public void ShouldResetPartitionAndExtendedCsdTransferOnCmd0()
        {
            const uint sector = 0x1000 / 512;
            var uda = new byte[] { 0x55, 0x44, 0x41, 0x30 };
            var boot = new byte[] { 0x42, 0x4F, 0x4F, 0x54 };

            using(var card = new SDCard(0x1000000, emmc: true))
            {
                card.Reset();
                WriteAt(card, sector, uda);
                SwitchPartition(card, 1);
                WriteAt(card, sector, boot);

                card.HandleCommand(8, 0);
                card.ReadData(1);
                card.HandleCommand(0, 0);

                CollectionAssert.AreEqual(uda, ReadAt(card, sector, uda.Length));
            }
        }

        [Test]
        public void ShouldUseSectorAddressingForEmmc()
        {
            var marker = new byte[] { 0xAB, 0xCD, 0xEF, 0x01 };

            using(var card = new SDCard(0x1000000, emmc: true))
            {
                card.Reset();
                WriteAt(card, 2, marker);
                CollectionAssert.AreEqual(marker, ReadAt(card, 2, marker.Length));
                CollectionAssert.AreNotEqual(marker, ReadAt(card, 0, marker.Length));
            }
        }

        [Test]
        public void ShouldRejectUnavailableGeneralPurposePartition()
        {
            using(var card = new SDCard(0x1000000, emmc: true))
            {
                card.Reset();

                // 4 is the first general-purpose partition; we only model user/boot/RPMB (0-3), so the switch must fail.
                var response = SwitchPartition(card, 4);

                Assert.That(response.AsUInt32() & (1u << 7), Is.Not.Zero, "SWITCH_ERROR should be set");
                card.HandleCommand(8, 0);
                Assert.That(card.ReadData(512)[179] & 0x7, Is.Zero, "PARTITION_ACCESS should remain unchanged");
            }
        }

        // eMMC CMD6 SWITCH, Access=Write byte (3), Index=PARTITION_CONFIG (179), Value=access.
        private static BitStream SwitchPartition(SDCard card, uint access)
        {
            return card.HandleCommand(6, (3u << 24) | (179u << 16) | (access << 8));
        }

        private static void WriteAt(SDCard card, uint sector, byte[] data)
        {
            card.HandleCommand(24, sector);
            card.WriteData(data);
        }

        private static byte[] ReadAt(SDCard card, uint sector, int length)
        {
            card.HandleCommand(17, sector);
            return card.ReadData((uint)length);
        }
    }
}
