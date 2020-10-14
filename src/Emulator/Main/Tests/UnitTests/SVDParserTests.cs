//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using NUnit.Framework;
using Antmicro.Renode.Utilities;
using System.IO;
using Antmicro.Renode.Peripherals.Bus;
using Machine = Antmicro.Renode.Core.Machine;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class SVDParserTests
    {
        [OneTimeSetUp]
        public void Init()
        {
            currentMachine = new Machine();
        }

        #region Format tests

        [Test]
        public void ShouldThrowOnNonexistingFile()
        {
            Assert.Throws<RecoverableException>(() =>
            {
                currentMachine = new Machine();
                device = new SVDParser(Path.Combine("invalid", "path.svd"), currentMachine.SystemBus);
            });
        }

        [Test]
        public void ShouldThrowOnEmptyFile()
        {
            Assert.Throws<RecoverableException>(() =>
            {
                SetUpDeviceWithString("");
            });
        }

        [Test]
        public void ShouldThrowOnInvalidXml()
        {
            Assert.Throws<RecoverableException>(() =>
            {
                SetUpDeviceWithString("Lorem ipsum...");
            });
        }

        [Test]
        public void ShouldThrowOnInvalidSvd()
        {
            Assert.Throws<RecoverableException>(() =>
            {
                SetUpDeviceWithString(@"<?xml version=""1.0"" encoding=""utf-8"" standalone=""no""?> 
                    <invalidTag>
                    </invalidTag>
                ");
            });
        }

        [Test]
        public void ShouldThrowOnDeviceWithNoMandatoryFields()
        {
            Assert.Throws<RecoverableException>(() =>
            {
                SetUpDeviceWithString(@"<?xml version=""1.0"" encoding=""utf-8"" standalone=""no""?> 
                    <device>
                    </device>
                ");
            });
        }

        [Test]
        public void ShouldThrowOnDeviceWithNoPeripheralsTag()
        {
            Assert.Throws<RecoverableException>(() =>
            {
                SetUpDeviceWithInfix("");
            });
        }

        [Test]
        public void ShouldThrowOnDeviceWithNotEveryMandatoryField()
        {
            // Both tags <description> and <name> are mandatory.
            string[] mandatories = { "descripion", "name" };
            foreach(var item in mandatories)
            {
                Assert.Throws<RecoverableException>(() =>
                {
                    SetUpDeviceWithString($@"<?xml version=""1.0"" encoding=""utf-8"" standalone=""no""?>
                        <device>
                            <{item}>value</{item}>
                        </device>"
                    );
                });
            }
        }

        [Test]
        public void ShouldThrowOnCpuWithoutEndianness()
        {
            // If cpu is defined the tag <endian> is mandatory.
            Assert.Throws<RecoverableException>(() =>
            {
                SetUpDeviceWithString($@"<?xml version=""1.0"" encoding=""utf-8"" standalone=""no""?>
                    <device>
                        <description>Test description</description>
                        <name>Test name</name>
                        <cpu>
                        </cpu>
                    </device>"
                );
            });
        }

        [Test]
        public void ShouldThrowOnPeripheralWithNotEveryMandatoryField([Values("name", "baseAddress")] string tag)
        {
            // Both tags <name> and <baseAddress> are mandatory.
            // 0x1000 is used as a field value so we do not have to care about field types in this test.
            Assert.Throws<RecoverableException>(() =>
            {
                SetUpDeviceWithInfix($@"
                    <peripherals>  
                        <peripheral>
                            <{tag}>0x1000</{tag}>
                        </peripheral>
                    </peripherals>
                ");
            });
        }

        [Test]
        public void ShouldThrowOnClusterWithoutMandatoryFields()
        {
            // Tag <addressOffset> is mandatory.
            Assert.Throws<RecoverableException>(() =>
            {
                SetUpDeviceWithInfix(@"
                    <peripherals>
                        <peripheral>
                            <name>Peripheral1</name>
                            <baseAddress>0x1000</baseAddress>
                            <registers>
                                <cluster>
                                    <!--addressOffset and name missing-->
                                    <register>
                                        <name>REG1</name>
                                        <addressOffset>0x0</addressOffset>
                                        <resetValue>0</resetValue>
                                    </register>
                                </cluster>
                            </registers>
                        </peripheral>
                    </peripherals>
                ");
            });
        }

        [Test]
        public void ShouldThrowOnRegisterWithNotEveryMandatoryField([Values("name", "addressOffset")] string tag)
        {
            // Both tags <name> and <addressOffset> are mandatory.
            // 0x1000 is used as a field value so we do not have to care about field types in this test.
            Assert.Throws<RecoverableException>(() =>
            {
                SetUpDeviceWithInfix($@"
                    <peripherals>  
                        <peripheral>
                            <name>Peripheral1</name>
                            <baseAddress>0x1000</baseAddress>
                            <registers>
                                <cluster>
                                    <addressOffset>0</addressOffset>
                                    <register>
                                        <{tag}>0x1000</{tag}>
                                    </register>
                                </cluster>
                            </registers>
                        </peripheral>
                    </peripherals>
                ");
            });
        }

        [Test]
        public void ShouldThrowOnDeviceWithInvalidSize()
        {
            Assert.Throws<RecoverableException>(() =>
            {
                SetUpDeviceWithString(@"<?xml version=""1.0"" encoding=""utf-8"" standalone=""no""?>
                    <device>
                        <description>Test description</description>
                        <name>Test name</name>
                        <size>invalidValue</size>
                    </device>"
                );
            });
        }

        [Test]
        public void ShouldThrowOnDeviceWithInvalidResetValue()
        {
            Assert.Throws<RecoverableException>(() =>
            {
                SetUpDeviceWithString(@"<?xml version=""1.0"" encoding=""utf-8"" standalone=""no""?>
                    <device>
                        <description>Test description</description>
                        <name>Test name</name>
                        <resetValue>invalidValue</resetValue>
                        <peripherals>
                        </peripherals>
                    </device>"
                );
            });
        }

        [Test]
        public void ShouldThrowOnDeviceWithInvalidAccess()
        {
            Assert.Throws<RecoverableException>(() =>
            {
                SetUpDeviceWithString(@"<?xml version=""1.0"" encoding=""utf-8"" standalone=""no""?>
                    <device>
                        <description>Test description</description>
                        <name>Test name</name>
                        <access>invalidValue</access>
                    </device>"
                );
            });
        }

        [Test]
        public void ShouldThrowOnCpuWithInvalidEndianness()
        {
            Assert.Throws<RecoverableException>(() =>
            {
                SetUpDeviceWithString($@"<?xml version=""1.0"" encoding=""utf-8"" standalone=""no""?>
                    <device>
                        <description>Test description</description>
                        <name>Test name</name>
                        <cpu>
                            <endian>invalidValue</endian>
                        </cpu>
                        <peripherals>
                        </peripherals>
                    </device>"
                );
            });
        }

        [Test]
        public void ShouldThrowOnRegisterWithoutDeterminedSizeAndResetValue()
        {
            Assert.Throws<RecoverableException>(() =>
            {
                SetUpDeviceWithString($@"<?xml version=""1.0"" encoding=""utf-8"" standalone=""no""?>
                <device>
                    <description>Test description</description>
                    <name>Test name</name>
                    <access>read-write</access>
                    <peripherals>
                        <peripheral>
                            <name>name</name>
                            <baseAddress>0</baseAddress>
                            <registers>
                                <register>
                                    <name>REG1</name>
                                    <addressOffset>0</addressOffset>
                                </register>
                            </registers>
                        </peripheral>
                    </peripherals>
                </device>"
                );
            });
        }

        #endregion Format tests

        #region Read tests

        [Test]
        public void ShouldReadValueFromRegister()
        {
            var variableValue = 0xDEADBEEF;
            SetUpDeviceWithInfix($@"
                <peripherals>  
                    <peripheral>
                        <name>Peripheral1</name>
                        <baseAddress>0x1000</baseAddress>
                        <registers>
                            <register>
                                <name>REG1</name>
                                <addressOffset>0x0</addressOffset>
                                <resetValue>{variableValue}</resetValue>
                            </register>
                        </registers>
                    </peripheral>
                </peripherals>
            ");
            Assert.IsTrue(device.TryReadAccess(0x1000, out var result, SysbusAccessWidth.DoubleWord));
            Assert.AreEqual(result, variableValue);
        }

        [Test]
        public void ShouldReadValueFromRegisterInBigEndian()
        {
            var variableValue = 0xDEADBEEF;
            byte[] bytes = BitConverter.GetBytes(variableValue);
            SetUpDeviceWithInfix($@"
                <peripherals>  
                    <peripheral>
                        <name>Peripheral1</name>
                        <baseAddress>0x1000</baseAddress>
                        <registers>
                            <register>
                                <name>REG1</name>
                                <addressOffset>0x0</addressOffset>
                                <resetValue>{variableValue}</resetValue>
                            </register>
                        </registers>
                    </peripheral>
                </peripherals>
                ",
                false
            );

            byte[] newBytes = { bytes[3], bytes[2], bytes[1], bytes[0] };
            var expectedValue = BitConverter.ToUInt32(newBytes, 0);
            Assert.IsTrue(device.TryReadAccess(0x1000, out var result, SysbusAccessWidth.DoubleWord));
            Assert.AreEqual(result, expectedValue);
        }

        [Test]
        public void ShouldHandleDifferentAccessPermissions([Values("write-only", "read-only", "read-write", "read-writeOnce", "writeOnce")] string access)
        {
            SetUpDeviceWithInfix($@"
                <peripherals>  
                    <peripheral>
                        <name>Peripheral1</name>
                        <baseAddress>0x1000</baseAddress>
                        <registers>
                            <register>
                                <name>REG1</name>
                                <addressOffset>0x0</addressOffset>
                                <resetValue>0x01234567</resetValue>
                                <access>{access}</access>
                            </register>
                        </registers>
                    </peripheral>
                </peripherals>
            ");
            var returnValue = device.TryReadAccess(0x1000, out var result, SysbusAccessWidth.DoubleWord);
            if(access == "write-only" || access == "writeOnce")
            {
                Assert.AreEqual(result, 0);
                Assert.IsTrue(returnValue);
            }
            else
            {
                Assert.AreEqual(result, 0x01234567);
                Assert.IsTrue(returnValue);
            }
        }

        [Test]
        public void ShouldReadFromRegistersOfDifferentSizes()
        {
            var variableValue = 0xDEADBEEF;
            var maxSize = 32;
            for(var i = 1; i < maxSize; i++)
            {
                SetUpDeviceWithInfix($@"
                    <peripherals>  
                        <peripheral>
                            <name>Peripheral1</name>
                            <baseAddress>0x1000</baseAddress>
                            <registers>
                                <register>
                                   <name>REG1</name>
                                    <addressOffset>0x0</addressOffset>
                                    <size>{i}</size>
                                    <resetValue>{variableValue}</resetValue>
                                    <access>read-write</access>
                                </register>
                            </registers>
                        </peripheral>
                    </peripherals>
                ");
                var mask = (uint)((1ul << i) - 1);
                var expectedValue = variableValue & mask;
                Assert.IsTrue(device.TryReadAccess(0x1000, out var result, SysbusAccessWidth.DoubleWord));
                Assert.AreEqual(result, expectedValue);
            }
        }

        [Test]
        public void ShouldReadFromUnalignedOffsetInOnePeripheral()
        {
            byte[] bytes = { 11, 22, 33, 44, 55, 66, 77, 88 };
            SetUpDeviceWithInfix($@"
                <peripherals>  
                    <peripheral>
                        <access>read-write</access>
                        <name>Peripheral1</name>
                        <baseAddress>0x1000</baseAddress>
                        <registers>
                            <register>
                                <name>REG1</name>
                                <addressOffset>0x0</addressOffset>
                                <resetValue>{BitConverter.ToUInt32(bytes, 0)}</resetValue>
                                <size>32</size>
                            </register>
                            <register>
                                <name>REG2</name>
                                <addressOffset>0x4</addressOffset>
                                <resetValue>{bytes[4]}</resetValue>
                                <size>8</size>
                            </register>
                            <register>
                                <name>REG3</name>
                                <addressOffset>0x5</addressOffset>
                                <resetValue>{bytes[5]}</resetValue>
                                <size>8</size>
                            </register>
                            <register>
                                <name>REG4</name>
                                <addressOffset>0x6</addressOffset>
                                <resetValue>{BitConverter.ToInt16(bytes, 6)}</resetValue>
                                <size>16</size>
                            </register>
                        </registers>
                    </peripheral>
                </peripherals>
            ");

            for(var i = -3; i < 8; i++)
            {
                var readingAddress = (ulong)(0x1000 + i);
                var expectedBytes = new byte[4];
                for(var j = 0u; j < 4; j++)
                {
                    if(readingAddress + j >= 0x1000 && readingAddress + j < 0x1008)
                    {
                        expectedBytes[j] = bytes[readingAddress + j - 0x1000];
                    }
                    else
                    {
                        expectedBytes[j] = 0;
                    }
                }
                var expectedValue = BitConverter.ToUInt32(expectedBytes, 0);
                Assert.IsTrue(device.TryReadAccess(readingAddress, out var result, SysbusAccessWidth.DoubleWord));
                Assert.AreEqual(result, expectedValue);
            }
        }

        [Test]
        public void ShouldReadFromUnalignedOffsetInManyPeripherals()
        {
            byte[] bytes = { 11, 22, 33, 44, 55, 66, 77, 88 };
            SetUpDeviceWithInfix($@"
                <peripherals>  

                    <peripheral>
                        <access>read-write</access>
                        <name>Peripheral1</name>
                        <baseAddress>0x1000</baseAddress>
                        <registers>
                            <register>
                                <name>REG1</name>
                                <addressOffset>0x0</addressOffset>
                                <resetValue>{BitConverter.ToUInt32(bytes, 0)}</resetValue>
                                <size>32</size>
                            </register>
                        </registers>
                    </peripheral>

                    <peripheral>
                        <access>read-write</access>
                        <name>Peripheral2</name>
                        <baseAddress>0x1004</baseAddress>
                        <registers>
                            <register>
                                <name>REG2</name>
                                <addressOffset>0</addressOffset>
                                <resetValue>{bytes[4]}</resetValue>
                                <size>8</size>
                            </register>
                            <register>
                                <name>REG3</name>
                                <addressOffset>1</addressOffset>
                                <resetValue>{bytes[5]}</resetValue>
                                <size>8</size>
                            </register>
                        </registers>
                    </peripheral>

                    <peripheral>
                        <access>read-write</access>
                        <name>Peripheral3</name>
                        <baseAddress>0x1006</baseAddress>
                        <registers>
                            <register>
                                <name>REG4</name>
                                <addressOffset>0</addressOffset>
                                <resetValue>{BitConverter.ToInt16(bytes, 6)}</resetValue>
                                <size>16</size>
                            </register>
                        </registers>
                    </peripheral>
                </peripherals>
            ");

            for(var i = -3; i < 8; i++)
            {
                var readingAddress = (ulong)(0x1000 + i);
                var expectedBytes = new byte[4];
                for(var j = 0u; j < 4; j++)
                {
                    if(readingAddress + j >= 0x1000 && readingAddress + j < 0x1008)
                    {
                        expectedBytes[j] = bytes[readingAddress + j - 0x1000];
                    }
                    else
                    {
                        expectedBytes[j] = 0;
                    }
                }
                var expectedValue = BitConverter.ToUInt32(expectedBytes, 0);

                Assert.IsTrue(device.TryReadAccess(readingAddress, out var result, SysbusAccessWidth.DoubleWord));
                Assert.AreEqual(result, expectedValue);
            }
        }

        [Test]
        public void ShouldReadFromUnalignedOffsetInOnePeripheralInBigEndian()
        {
            byte[] bytes = { 11, 22, 33, 44, 55, 66, 77, 88 };
            SetUpDeviceWithInfix($@"
                <peripherals>  
                    <peripheral>
                        <access>read-write</access>
                        <name>Peripheral1</name>
                        <baseAddress>0x1000</baseAddress>
                        <registers>
                            <register>
                                <name>REG1</name>
                                <addressOffset>0x0</addressOffset>
                                <resetValue>{BitConverter.ToUInt32(bytes, 0)}</resetValue>
                                <size>32</size>
                            </register>
                            <register>
                                <name>REG2</name>
                                <addressOffset>0x4</addressOffset>
                                <resetValue>{bytes[4]}</resetValue>
                                <size>8</size>
                            </register>
                            <register>
                                <name>REG3</name>
                                <addressOffset>0x5</addressOffset>
                                <resetValue>{bytes[5]}</resetValue>
                                <size>8</size>
                            </register>
                            <register>
                                <name>REG4</name>
                                <addressOffset>0x6</addressOffset>
                                <resetValue>{BitConverter.ToInt16(bytes, 6)}</resetValue>
                                <size>16</size>
                            </register>
                        </registers>
                    </peripheral>
                </peripherals>
                ",
                false
            );
            var readingAddress = (ulong)(0x1000 - 3);
            for(var i = -3; i < 8; i++)
            {
                var expectedBytes = new byte[4];
                for(var j = 0u; j < 4; j++)
                {
                    var tmpAddres = readingAddress + j;
                    if(tmpAddres >= 0x1000 && tmpAddres < 0x1008)
                    {
                        if(tmpAddres <= 0x1003)
                        {
                            var offset = tmpAddres - 0x1000;
                            var bigEndianAddress = 0x1003 - offset;
                            expectedBytes[j] = bytes[bigEndianAddress - 0x1000];
                        }
                        else if(tmpAddres == 0x1004)
                        {
                            expectedBytes[j] = bytes[tmpAddres - 0x1000];
                        }
                        else if(tmpAddres == 0x1005)
                        {
                            expectedBytes[j] = bytes[tmpAddres - 0x1000];
                        }
                        else
                        {
                            var offset = tmpAddres - 0x1006;
                            var bigEndianAddress = 0x1007 - offset;
                            expectedBytes[j] = bytes[bigEndianAddress - 0x1000];
                        }
                    }
                    else
                    {
                        expectedBytes[j] = 0;
                    }
                }
                var expectedValue = BitConverter.ToUInt32(expectedBytes, 0);
                Assert.IsTrue(device.TryReadAccess(readingAddress, out var result, SysbusAccessWidth.DoubleWord));
                Assert.AreEqual(result, expectedValue);
                readingAddress++;
            }
        }

        [Test]
        public void ShouldReadFromUnalignedOffsetInOnePeripheralWithWriteOnlyRegister()
        {
            byte[] bytes = { 11, 22, 33, 44, 55, 66, 77, 88 };
            SetUpDeviceWithInfix($@"
                <peripherals>  
                    <peripheral>
                        <access>read-write</access>
                        <name>Peripheral1</name>
                        <baseAddress>0x1000</baseAddress>
                        <registers>
                            <register>
                                <name>REG1</name>
                                <addressOffset>0x0</addressOffset>
                                <resetValue>{BitConverter.ToUInt32(bytes, 0)}</resetValue>
                                <size>32</size>
                            </register>
                            <register>
                                <name>REG2</name>
                                <addressOffset>0x4</addressOffset>
                                <resetValue>{bytes[4]}</resetValue>
                                <access>write-only</access>
                                <size>8</size>
                            </register>
                            <register>
                                <name>REG3</name>
                                <addressOffset>0x5</addressOffset>
                                <resetValue>{bytes[5]}</resetValue>
                                <access>write-only</access>
                                <size>8</size>
                            </register>
                            <register>
                                <name>REG4</name>
                                <addressOffset>0x6</addressOffset>
                                <resetValue>{BitConverter.ToInt16(bytes, 6)}</resetValue>
                                <size>16</size>
                            </register>
                        </registers>
                    </peripheral>
                </peripherals>
                ",
                false
            );
            var readingAddress = (ulong)(0x1000 - 3);
            for(var i = -3; i < 8; i++)
            {
                var expectedBytes = new byte[4];
                for(var j = 0u; j < 4; j++)
                {
                    var tmpAddres = readingAddress + j;
                    if(tmpAddres >= 0x1000 && tmpAddres < 0x1008)
                    {
                        if(tmpAddres == 0x1004 || tmpAddres == 0x1005)
                        {
                            expectedBytes[j] = 0;
                        }
                        else if(tmpAddres <= 0x1003)
                        {
                            var offset = tmpAddres - 0x1000;
                            var bigEndianAddress = 0x1003 - offset;
                            expectedBytes[j] = bytes[bigEndianAddress - 0x1000];
                        }
                        else if(tmpAddres == 0x1004)
                        {
                            expectedBytes[j] = bytes[tmpAddres - 0x1000];
                        }
                        else if(tmpAddres == 0x1005)
                        {
                            expectedBytes[j] = bytes[tmpAddres - 0x1000];
                        }
                        else
                        {
                            var offset = tmpAddres - 0x1006;
                            var bigEndianAddress = 0x1007 - offset;
                            expectedBytes[j] = bytes[bigEndianAddress - 0x1000];
                        }
                    }
                    else
                    {
                        expectedBytes[j] = 0;
                    }
                }
                var expectedValue = BitConverter.ToUInt32(expectedBytes, 0);
                Assert.IsTrue(device.TryReadAccess(readingAddress, out var result, SysbusAccessWidth.DoubleWord));
                Assert.AreEqual(result, expectedValue);
                readingAddress++;
            }
        }

        #endregion Read tests

        #region Inheritance tests

        [Test]
        public void ShouldConcatenateAddressOffsets()
        {
            var baseAddress = 0x10000;
            int[] addressOffset = { 0x1000, 0x100, 0x10 };
            var finalAddressOffset = 0x11110u;

            SetUpDeviceWithInfix($@"
                <peripherals>  
                    <peripheral>
                        <name>Peripheral1</name>
                        <baseAddress>{baseAddress}</baseAddress>
                        <access>read-write</access>
                        <registers>
                            <cluster>
                                <name>C0</name>
                                <addressOffset>{addressOffset[0]}</addressOffset>
                                <cluster>
                                    <name>C1</name>
                                    <addressOffset>{addressOffset[1]}</addressOffset>
                                    <register>
                                        <name>REG1</name>
                                        <addressOffset>{addressOffset[2]}</addressOffset>
                                        <resetValue>0x1000</resetValue>
                                    </register>
                                </cluster>
                            </cluster>
                        </registers>
                    </peripheral>
                </peripherals>
            ");
            Assert.IsTrue(device.TryReadAccess(finalAddressOffset, out var result, SysbusAccessWidth.DoubleWord));
            Assert.AreEqual(result, 0x1000);
        }

        [Test]
        public void ShouldInheritSettingsFromParentPeripheral()
        {
            SetUpDeviceWithInfix(@"
                <peripherals>  
                    <peripheral>
                        <name>Peripheral1</name>
                        <baseAddress>0x1000</baseAddress>
                        <size>11</size>
                        <resetValue>0xA5445E63</resetValue>
                        <access>read-writeOnce</access>
                        <registers>
                            <register>
                                <name>REG1</name>
                                <addressOffset>0x0</addressOffset>
                            </register>
                            <register>
                                <name>REG2</name>
                                <addressOffset>0x10</addressOffset>
                                <size>27</size>
                            </register>
                            <register>
                                <name>REG3</name>
                                <addressOffset>0x20</addressOffset>
                                <resetValue>0x12345678</resetValue>
                                <size>27</size>
                            </register>
                        </registers>
                    </peripheral>
                </peripherals>
            ");
            Assert.IsTrue(device.TryReadAccess(0x1000, out var result, SysbusAccessWidth.DoubleWord));
            Assert.AreEqual(result, 0x00000663);
            Assert.IsTrue(device.TryReadAccess(0x1010, out result, SysbusAccessWidth.DoubleWord));
            Assert.AreEqual(result, 0x05445E63);
            Assert.IsTrue(device.TryReadAccess(0x1020, out result, SysbusAccessWidth.DoubleWord));
            Assert.AreEqual(result, 0x02345678);
        }

        [Test]
        public void ShouldInheritSettingsFromParentCluster()
        {
            SetUpDeviceWithInfix(@"
                <peripherals>  
                    <peripheral>
                        <name>Peripheral1</name>
                        <baseAddress>0x1000</baseAddress>
                        <size>32</size>
                        <resetValue>0xFFFFFFFF</resetValue>
                        <access>read-writeOnce</access>
                        <registers>
                            <cluster>
                                <name>Cluster1</name>
                                <addressOffset>0x100</addressOffset>
                                <resetValue>0xA5445E63</resetValue>
                                <size>11</size>
                                <register>
                                    <name>REG1</name>
                                    <addressOffset>0x0</addressOffset>
                                </register>
                                <register>
                                    <name>REG2</name>
                                    <addressOffset>0x10</addressOffset>
                                    <size>27</size>
                                </register>
                                <register>
                                    <name>REG3</name>
                                    <addressOffset>0x20</addressOffset>
                                    <resetValue>0x12345678</resetValue>
                                    <size>27</size>
                                </register>
                            </cluster>
                        </registers>
                    </peripheral>
                </peripherals>
            ");
            Assert.IsTrue(device.TryReadAccess(0x1100, out var result, SysbusAccessWidth.DoubleWord));
            Assert.AreEqual(result, 0x00000663);
            Assert.IsTrue(device.TryReadAccess(0x1110, out result, SysbusAccessWidth.DoubleWord));
            Assert.AreEqual(result, 0x05445E63);
            Assert.IsTrue(device.TryReadAccess(0x1120, out result, SysbusAccessWidth.DoubleWord));
            Assert.AreEqual(result, 0x02345678);
        }

        [Test]
        public void ShouldInheritSettingsFromParentClusterAndPeripheral()
        {
            SetUpDeviceWithInfix(@"
                <peripherals>  
                    <peripheral>
                        <name>Peripheral1</name>
                        <baseAddress>0x1000</baseAddress>
                        <size>11</size>
                        <resetValue>0xFFFFFFFF</resetValue>
                        <access>read-writeOnce</access>
                        <registers>
                            <register>
                                <name>REG1</name>
                                <addressOffset>0x0</addressOffset>
                            </register>
                            <cluster>
                                <name>Cluster1</name>
                                <addressOffset>0x100</addressOffset>
                                <resetValue>0xA5445E63</resetValue>
                                <size>27</size>
                                <register>
                                    <name>REG2</name>
                                    <addressOffset>0x10</addressOffset>
                                </register>
                                <register>
                                    <name>REG3</name>
                                    <addressOffset>0x20</addressOffset>
                                    <resetValue>0x12345678</resetValue>
                                </register>
                            </cluster>
                        </registers>
                    </peripheral>
                </peripherals>
            ");
            Assert.IsTrue(device.TryReadAccess(0x1000, out var result, SysbusAccessWidth.DoubleWord));
            Assert.AreEqual(result, 0x000007FF);
            Assert.IsTrue(device.TryReadAccess(0x1110, out result, SysbusAccessWidth.DoubleWord));
            Assert.AreEqual(result, 0x05445E63);
            Assert.IsTrue(device.TryReadAccess(0x1120, out result, SysbusAccessWidth.DoubleWord));
            Assert.AreEqual(result, 0x02345678);
        }

        #endregion Inheritance tests

        #region Deriving tests

        [Test]
        public void ShouldThrowOnDerivingFromNonexistingRegister()
        {
            Assert.Throws<RecoverableException>(() =>
            {
                SetUpDeviceWithInfix(@"
                    <peripherals>  
                        <peripheral>
                            <name>Peripheral1</name>
                            <baseAddress>0x1000</baseAddress>
                            <access>read-write</access>
                            <registers>
                                <register>
                                    <name>REG1</name>
                                    <addressOffset>0x0</addressOffset>
                                    <size>32</size>
                                    <resetValue>0xF2468ACE</resetValue>
                                </register>
                                <register derivedFrom=""invalidObject"">
                                    <name>REG2</name>
                                    <addressOffset>0x10</addressOffset>
                                </register>
                            </registers>
                        </peripheral>
                    </peripherals>
                ");
                device.TryReadAccess(0x1010, out var result, SysbusAccessWidth.DoubleWord);
            });
        }

        [Test]
        public void ShouldDeriveFromRegisterInTheSameScope()
        {
            SetUpDeviceWithInfix(@"
                <peripherals>  
                    <peripheral>
                        <name>Peripheral1</name>
                        <baseAddress>0x1000</baseAddress>
                        <access>read-write</access>
                        <registers>
                            <register>
                                <name>REG1</name>
                                <addressOffset>0x0</addressOffset>
                                <size>32</size>
                                <resetValue>0xF2468ACE</resetValue>
                            </register>
                            <register derivedFrom=""REG1"">
                                <name>REG2</name>
                                <addressOffset>0x10</addressOffset>
                            </register>
                        </registers>
                    </peripheral>
                </peripherals>
            ");
            Assert.IsTrue(device.TryReadAccess(0x1010, out var result, SysbusAccessWidth.DoubleWord));
            Assert.AreEqual(result, 0xF2468ACE);
        }

        [Test]
        public void ShouldDeriveFromRegisterInDifferentCluster()
        {
            SetUpDeviceWithInfix(@"
                <peripherals>  
                    <peripheral>
                        <name>Peripheral1</name>
                        <baseAddress>0x1000</baseAddress>
                        <access>read-write</access>
                        <registers>
                            <cluster>
                                <name>C0</name>
                                <addressOffset>0x0</addressOffset>
                                <register>
                                    <name>REG1</name>
                                    <addressOffset>0x0</addressOffset>
                                    <size>32</size>
                                    <resetValue>0xF2468ACE</resetValue>
                                </register>
                            </cluster>
                            <register derivedFrom=""Peripheral1.C0.REG1"">
                                <name>REG2</name>
                                <addressOffset>0x10</addressOffset>
                            </register>
                        </registers>
                    </peripheral>
                </peripherals>
            ");
            Assert.IsTrue(device.TryReadAccess(0x1010, out var result, SysbusAccessWidth.DoubleWord));
            Assert.AreEqual(result, 0xF2468ACE);
        }

        [Test]
        public void ShouldDeriveFromRegisterInDifferentPeripheral()
        {
            SetUpDeviceWithInfix(@"
                <peripherals>  
                    <peripheral>
                        <name>Peripheral1</name>
                        <baseAddress>0x1000</baseAddress>
                        <access>read-write</access>
                        <registers>
                            <cluster>
                                <name>C0</name>
                                <size>32</size>
                                <addressOffset>0x0</addressOffset>
                                <register>
                                    <name>REG1</name>
                                    <addressOffset>0x0</addressOffset>
                                    <resetValue>0xF2468ACE</resetValue>
                                </register>
                            </cluster>
                        </registers>
                    </peripheral>
                    <peripheral>
                        <name>Peripheral2</name>
                        <baseAddress>0x2000</baseAddress>
                        <access>read-write</access>
                        <registers>
                            <register derivedFrom=""Peripheral1.C0.REG1"">
                                <name>REG2</name>
                                <addressOffset>0x10</addressOffset>
                            </register>
                        </registers>
                    </peripheral>
                </peripherals>
            ");
            Assert.IsTrue(device.TryReadAccess(0x2010, out var result, SysbusAccessWidth.DoubleWord));
            Assert.AreEqual(result, 0xF2468ACE);
        }

        #endregion Deriving tests

        private void SetUpDeviceWithInfix(string infix, bool littleEndian = true)
        {
            var fileName = TemporaryFilesManager.Instance.GetTemporaryFile();
            File.WriteAllText(fileName, (littleEndian ? Prefix : BigEndianPrefix) + infix + Postfix);
            device = new SVDParser(fileName, currentMachine.SystemBus);
        }

        private void SetUpDeviceWithString(string content)
        {
            var fileName = TemporaryFilesManager.Instance.GetTemporaryFile();
            File.WriteAllText(fileName, content);
            device = new SVDParser(fileName, currentMachine.SystemBus);
        }

        private Machine currentMachine;
        private SVDParser device;

        private const string Prefix = @"<?xml version=""1.0"" encoding=""utf-8"" standalone=""no""?>
            <device>
                <description>Test description</description>
                <name>Test name</name>
                <size>32</size>
                <access>read-write</access>
                <resetValue>0xA5A5A5A5</resetValue>
        ";

        private const string BigEndianPrefix = @"<?xml version=""1.0"" encoding=""utf-8"" standalone=""no""?>
            <device>
                <description>Test description</description>
                <name>Test name</name>
                <size>32</size>
                <access>read-write</access>
                <resetValue>0xA5A5A5A5</resetValue>
                <cpu>
                    <endian>big</endian>
                </cpu>
        ";

        private const string Postfix = @"
            </device>
        ";
    }
}
