//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Peripherals.CFU;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Binding;
using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Peripherals.CPU
{
    public abstract class BaseRiscV : TranslationCPU, IPeripheralContainer<ICFU, NumberRegistrationPoint<int>>
    {
        protected BaseRiscV(IRiscVTimeProvider timeProvider, uint hartId, string cpuType, Machine machine, PrivilegeArchitecture privilegeArchitecture, Endianess endianness, CpuBitness bitness, ulong? nmiVectorAddress = null, uint? nmiVectorLength = null, bool allowUnalignedAccesses = false, InterruptMode interruptMode = InterruptMode.Auto)
                : base(hartId, cpuType, machine, endianness, bitness)
        {
            HartId = hartId;
            this.timeProvider = timeProvider;
            this.privilegeArchitecture = privilegeArchitecture;
            shouldEnterDebugMode = true;
            nonstandardCSR = new Dictionary<ulong, NonstandardCSR>();
            customInstructionsMapping = new Dictionary<ulong, Action<UInt64>>();
            this.NMIVectorLength = nmiVectorLength;
            this.NMIVectorAddress = nmiVectorAddress;

            architectureSets = DecodeArchitecture(cpuType);
            EnableArchitectureVariants();

            if(this.NMIVectorAddress.HasValue && this.NMIVectorLength.HasValue && this.NMIVectorLength > 0)
            {
                this.Log(LogLevel.Noisy, "Non maskable interrupts enabled with paramters: {0} = {1}, {2} = {3}",
                        nameof(this.NMIVectorAddress), this.NMIVectorAddress, nameof(this.NMIVectorLength), this.NMIVectorLength);
                TlibSetNmiVector(this.NMIVectorAddress.Value, this.NMIVectorLength.Value);
            }
            else
            {
                this.Log(LogLevel.Noisy, "Non maskable interrupts disabled");
                TlibSetNmiVector(0, 0);
            }

            TlibAllowUnalignedAccesses(allowUnalignedAccesses ? 1 : 0);

            try
            {
                this.interruptMode = interruptMode;
                TlibSetInterruptMode((int)interruptMode);
            }
            catch(CpuAbortException)
            {
                throw new ConstructionException(string.Format("Unsupported interrupt mode: 0x{0:X}", interruptMode));
            }

            UserState = new Dictionary<string, object>();

            ChildCollection = new Dictionary<int, ICFU>();
        }

        public void Register(ICFU cfu, NumberRegistrationPoint<int> registrationPoint)
        {
            var isRegistered = ChildCollection.Where(x => x.Value.Equals(cfu)).Select(x => x.Key).ToList();
            if(isRegistered.Count != 0)
            {
                throw new RegistrationException("Can't register the same CFU twice.");
            }
            else if(ChildCollection.ContainsKey(registrationPoint.Address))
            {
                throw new RegistrationException("The specified registration point is already in use.");
            }

            ChildCollection.Add(registrationPoint.Address, cfu);
            machine.RegisterAsAChildOf(this, cfu, registrationPoint);
            cfu.ConnectedCpu = this;
        }

        public void Unregister(ICFU cfu)
        {
            var toRemove = ChildCollection.Where(x => x.Value.Equals(cfu)).Select(x => x.Key).ToList(); //ToList required, as we remove from the source
            foreach(var key in toRemove)
            {
                ChildCollection.Remove(key);
            }

            machine.UnregisterAsAChildOf(this, cfu);
        }

        public IEnumerable<NumberRegistrationPoint<int>> GetRegistrationPoints(ICFU cfu)
        {
            return ChildCollection.Keys.Select(x => new NumberRegistrationPoint<int>(x));
        }

        public IEnumerable<IRegistered<ICFU, NumberRegistrationPoint<int>>> Children
        {
            get
            {
                return ChildCollection.Select(x => Registered.Create(x.Value, new NumberRegistrationPoint<int>(x.Key)));
            }
        }

        public virtual void OnNMI(int number, bool value)
        {
            if(this.NMIVectorLength == null || this.NMIVectorAddress == null)
            {
                this.Log(LogLevel.Warning, "Non maskable interrupt not supported on this CPU. {0} or {1} not set",
                        nameof(this.NMIVectorAddress) , nameof(this.NMIVectorLength));
            }
            else
            {
                TlibSetNmi(number, value ? 1 : 0);
            }
        }

        public override void OnGPIO(int number, bool value)
        {

            // we don't log warning when value is false to handle gpio initial reset
            if(privilegeArchitecture >= PrivilegeArchitecture.Priv1_10 && IsValidInterruptOnlyInV1_09(number) && value)
            {
                this.Log(LogLevel.Warning, "Interrupt {0} not supported since Privileged ISA v1.10", (IrqType)number);
                return;
            }
            else if(IsUniplementedInterrupt(number) && value)
            {
                this.Log(LogLevel.Warning, "Interrupt {0} not supported", (IrqType)number);
                return;
            }

            TlibSetMipBit((uint)number, value ? 1u : 0u);
            base.OnGPIO(number, value);
        }

        public bool SupportsInstructionSet(InstructionSet set)
        {
            return TlibIsFeatureAllowed((uint)set) == 1;
        }

        public bool IsInstructionSetEnabled(InstructionSet set)
        {
            return TlibIsFeatureEnabled((uint)set) == 1;
        }

        public override void Reset()
        {
            base.Reset();
            ShouldEnterDebugMode = true;
            EnableArchitectureVariants();
            foreach(var key in simpleCSRs.Keys.ToArray())
            {
                simpleCSRs[key] = 0;
            }
            UserState.Clear();
        }

        public void RegisterCustomCSR(string name, uint number, PrivilegeLevel mode)
        {
            var customCSR = new SimpleCSR(name, number, mode);
            if(simpleCSRs.Keys.Any(x => x.Number == customCSR.Number))
            {
                throw new ConstructionException($"Cannot register CSR {customCSR.Name}, because its number 0x{customCSR.Number:X} is already registered");
            }
            simpleCSRs.Add(customCSR, 0);
            RegisterCSR(customCSR.Number, () => simpleCSRs[customCSR], value => simpleCSRs[customCSR] = value, name);
        }

        public void RegisterCSR(ulong csr, Func<ulong> readOperation, Action<ulong> writeOperation, string name = null)
        {
            nonstandardCSR.Add(csr, new NonstandardCSR(readOperation, writeOperation, name));
            if(TlibInstallCustomCSR(csr) == -1)
            {
                throw new ConstructionException($"CSR limit exceeded. Cannot register CSR 0x{csr:X}");
            }
        }

        public void SilenceUnsupportedInstructionSet(InstructionSet set, bool silent = true)
        {
            TlibMarkFeatureSilent((uint)set, silent ? 1 : 0u);
        }

        public bool InstallCustomInstruction(string pattern, Action<UInt64> handler)
        {
            if(pattern == null)
            {
                throw new ArgumentException("Pattern cannot be null");
            }
            if(handler == null)
            {
                throw new ArgumentException("Handler cannot be null");
            }

            if(pattern.Length != 64 && pattern.Length != 32 && pattern.Length != 16)
            {
                throw new RecoverableException($"Unsupported custom instruction length: {pattern.Length}. Supported values are: 16, 32, 64 bits");
            }

            var currentBit = pattern.Length - 1;
            var bitMask = 0uL;
            var bitPattern = 0uL;

            foreach(var p in pattern)
            {
                switch(p)
                {
                    case '0':
                        bitMask |= (1uL << currentBit);
                        break;

                    case '1':
                        bitMask |= (1uL << currentBit);
                        bitPattern |= (1uL << currentBit);
                        break;

                    default:
                        // all characters other than '0' or '1' are treated as 'any-value'
                        break;
                }

                currentBit--;
            }

            var length = (ulong)pattern.Length / 8;
            var id = TlibInstallCustomInstruction(bitMask, bitPattern, length);
            if(id == 0)
            {
                throw new ConstructionException($"Could not install custom instruction handler for length {length}, mask 0x{bitMask:X} and pattern 0x{bitPattern:X}");
            }

            customInstructionsMapping[id] = handler;
            return true;
        }

        public ulong Vector(uint registerNumber, uint elementIndex, ulong? value = null)
        {
            if(value.HasValue)
            {
                TlibSetVector(registerNumber, elementIndex, value.Value);
            }
            return TlibGetVector(registerNumber, elementIndex);
        }

        public CSRValidationLevel CSRValidation
        {
            get => (CSRValidationLevel)TlibGetCsrValidationLevel();

            set
            {
                TlibSetCsrValidationLevel((uint)value);
            }
        }

        public uint HartId
        {
            get
            {
                return TlibGetHartId();
            }

            set
            {
                TlibSetHartId(value);
            }
        }

        public uint GetRvvOpcodeCountFlag() {
            return TlibGetRvvOpcodeCountFlag();
        }

        public void EnableRvvOpcodeCount()
        {
            TlibEnableRvvOpcodeCount();
        }

        public void DisableRvvOpcodeCount()
        {
            TlibDisableRvvOpcodeCount();
        }

        public uint GetRvvOpcodeCount(uint rvv_opcode_index)
        {
            return TlibGetRvvOpcodeCount(rvv_opcode_index);
        }

        public void GetAllRvvOpcodeCounts()
        {
            var <uint, string> rvv_opcode_dict = new Dictionary<uint, string>();
            using (StreamWriter writer = new StreamWriter("~/rvv_opcode_count.csv"))
            {

                rvv_opcode_dict.Add(0, "vsetivli");
                rvv_opcode_dict.Add(1, "vsetvli");
                rvv_opcode_dict.Add(2, "vsetvl");
                rvv_opcode_dict.Add(3, "vlm.v");
                rvv_opcode_dict.Add(4, "vsm.v");
                rvv_opcode_dict.Add(5, "vle8.v");
                rvv_opcode_dict.Add(6, "vle16.v");
                rvv_opcode_dict.Add(7, "vle32.v");
                rvv_opcode_dict.Add(8, "vle64.v");
                rvv_opcode_dict.Add(9, "vle128.v");
                rvv_opcode_dict.Add(10, "vle256.v");
                rvv_opcode_dict.Add(11, "vle512.v");
                rvv_opcode_dict.Add(12, "vle1024.v");
                rvv_opcode_dict.Add(13, "vse8.v");
                rvv_opcode_dict.Add(14, "vse16.v");
                rvv_opcode_dict.Add(15, "vse32.v");
                rvv_opcode_dict.Add(16, "vse64.v");
                rvv_opcode_dict.Add(17, "vse128.v");
                rvv_opcode_dict.Add(18, "vse256.v");
                rvv_opcode_dict.Add(19, "vse512.v");
                rvv_opcode_dict.Add(20, "vse1024.v");
                rvv_opcode_dict.Add(21, "vluxei8.v");
                rvv_opcode_dict.Add(22, "vluxei16.v");
                rvv_opcode_dict.Add(23, "vluxei32.v");
                rvv_opcode_dict.Add(24, "vluxei64.v");
                rvv_opcode_dict.Add(25, "vluxei128.v");
                rvv_opcode_dict.Add(26, "vluxei256.v");
                rvv_opcode_dict.Add(27, "vluxei512.v");
                rvv_opcode_dict.Add(28, "vluxei1024.v");
                rvv_opcode_dict.Add(29, "vsuxei8.v");
                rvv_opcode_dict.Add(30, "vsuxei16.v");
                rvv_opcode_dict.Add(31, "vsuxei32.v");
                rvv_opcode_dict.Add(32, "vsuxei64.v");
                rvv_opcode_dict.Add(33, "vsuxei128.v");
                rvv_opcode_dict.Add(34, "vsuxei256.v");
                rvv_opcode_dict.Add(35, "vsuxei512.v");
                rvv_opcode_dict.Add(36, "vsuxei1024.v");
                rvv_opcode_dict.Add(37, "vlse8.v");
                rvv_opcode_dict.Add(38, "vlse16.v");
                rvv_opcode_dict.Add(39, "vlse32.v");
                rvv_opcode_dict.Add(40, "vlse64.v");
                rvv_opcode_dict.Add(41, "vlse128.v");
                rvv_opcode_dict.Add(42, "vlse256.v");
                rvv_opcode_dict.Add(43, "vlse512.v");
                rvv_opcode_dict.Add(44, "vlse1024.v");
                rvv_opcode_dict.Add(45, "vsse8.v");
                rvv_opcode_dict.Add(46, "vsse16.v");
                rvv_opcode_dict.Add(47, "vsse32.v");
                rvv_opcode_dict.Add(48, "vsse64.v");
                rvv_opcode_dict.Add(49, "vsse128.v");
                rvv_opcode_dict.Add(50, "vsse256.v");
                rvv_opcode_dict.Add(51, "vsse512.v");
                rvv_opcode_dict.Add(52, "vsse1024.v");
                rvv_opcode_dict.Add(53, "vloxei8.v");
                rvv_opcode_dict.Add(54, "vloxei16.v");
                rvv_opcode_dict.Add(55, "vloxei32.v");
                rvv_opcode_dict.Add(56, "vloxei64.v");
                rvv_opcode_dict.Add(57, "vloxei128.v");
                rvv_opcode_dict.Add(58, "vloxei256.v");
                rvv_opcode_dict.Add(59, "vloxei512.v");
                rvv_opcode_dict.Add(60, "vloxei1024.v");
                rvv_opcode_dict.Add(61, "vsoxei8.v");
                rvv_opcode_dict.Add(62, "vsoxei16.v");
                rvv_opcode_dict.Add(63, "vsoxei32.v");
                rvv_opcode_dict.Add(64, "vsoxei64.v");
                rvv_opcode_dict.Add(65, "vsoxei128.v");
                rvv_opcode_dict.Add(66, "vsoxei256.v");
                rvv_opcode_dict.Add(67, "vsoxei512.v");
                rvv_opcode_dict.Add(68, "vsoxei1024.v");
                rvv_opcode_dict.Add(69, "vle8ff.v");
                rvv_opcode_dict.Add(70, "vle16ff.v");
                rvv_opcode_dict.Add(71, "vle32ff.v");
                rvv_opcode_dict.Add(72, "vle64ff.v");
                rvv_opcode_dict.Add(73, "vle128ff.v");
                rvv_opcode_dict.Add(74, "vle256ff.v");
                rvv_opcode_dict.Add(75, "vle512ff.v");
                rvv_opcode_dict.Add(76, "vle1024ff.v");
                rvv_opcode_dict.Add(77, "vl1re8.v");
                rvv_opcode_dict.Add(78, "vl1re16.v");
                rvv_opcode_dict.Add(79, "vl1re32.v");
                rvv_opcode_dict.Add(80, "vl1re64.v");
                rvv_opcode_dict.Add(81, "vl2re8.v");
                rvv_opcode_dict.Add(82, "vl2re16.v");
                rvv_opcode_dict.Add(83, "vl2re32.v");
                rvv_opcode_dict.Add(84, "vl2re64.v");
                rvv_opcode_dict.Add(85, "vl4re8.v");
                rvv_opcode_dict.Add(86, "vl4re16.v");
                rvv_opcode_dict.Add(87, "vl4re32.v");
                rvv_opcode_dict.Add(88, "vl4re64.v");
                rvv_opcode_dict.Add(89, "vl8re8.v");
                rvv_opcode_dict.Add(90, "vl8re16.v");
                rvv_opcode_dict.Add(91, "vl8re32.v");
                rvv_opcode_dict.Add(92, "vl8re64.v");
                rvv_opcode_dict.Add(93, "vs1r.v");
                rvv_opcode_dict.Add(94, "vs2r.v");
                rvv_opcode_dict.Add(95, "vs4r.v");
                rvv_opcode_dict.Add(96, "vs8r.v");
                rvv_opcode_dict.Add(97, "vfadd.vf");
                rvv_opcode_dict.Add(98, "vfsub.vf");
                rvv_opcode_dict.Add(99, "vfmin.vf");
                rvv_opcode_dict.Add(100, "vfmax.vf");
                rvv_opcode_dict.Add(101, "vfsgnj.vf");
                rvv_opcode_dict.Add(102, "vfsgnjn.vf");
                rvv_opcode_dict.Add(103, "vfsgnjx.vf");
                rvv_opcode_dict.Add(104, "vfslide1up.vf");
                rvv_opcode_dict.Add(105, "vfslide1down.vf");
                rvv_opcode_dict.Add(106, "vfmv.s.f");
                rvv_opcode_dict.Add(107, "vfmerge.vfm");
                rvv_opcode_dict.Add(108, "vfmv.v.f");
                rvv_opcode_dict.Add(109, "vmfeq.vf");
                rvv_opcode_dict.Add(110, "vmfle.vf");
                rvv_opcode_dict.Add(111, "vmflt.vf");
                rvv_opcode_dict.Add(112, "vmfne.vf");
                rvv_opcode_dict.Add(113, "vmfgt.vf");
                rvv_opcode_dict.Add(114, "vmfge.vf");
                rvv_opcode_dict.Add(115, "vfdiv.vf");
                rvv_opcode_dict.Add(116, "vfrdiv.vf");
                rvv_opcode_dict.Add(117, "vfmul.vf");
                rvv_opcode_dict.Add(118, "vfrsub.vf");
                rvv_opcode_dict.Add(119, "vfmadd.vf");
                rvv_opcode_dict.Add(120, "vfnmadd.vf");
                rvv_opcode_dict.Add(121, "vfmsub.vf");
                rvv_opcode_dict.Add(122, "vfnmsub.vf");
                rvv_opcode_dict.Add(123, "vfmacc.vf");
                rvv_opcode_dict.Add(124, "vfnmacc.vf");
                rvv_opcode_dict.Add(125, "vfmsac.vf");
                rvv_opcode_dict.Add(126, "vfnmsac.vf");
                rvv_opcode_dict.Add(127, "vfwadd.vf");
                rvv_opcode_dict.Add(128, "vfwsub.vf");
                rvv_opcode_dict.Add(129, "vfwadd.wf");
                rvv_opcode_dict.Add(130, "vfwsub.wf");
                rvv_opcode_dict.Add(131, "vfwmul.vf");
                rvv_opcode_dict.Add(132, "vfwmacc.vf");
                rvv_opcode_dict.Add(133, "vfwnmacc.vf");
                rvv_opcode_dict.Add(134, "vfwmsac.vf");
                rvv_opcode_dict.Add(135, "vfwnmsac.vf");
                rvv_opcode_dict.Add(136, "vfadd.vv");
                rvv_opcode_dict.Add(137, "vfredusum.vs");
                rvv_opcode_dict.Add(138, "vfsub.vv");
                rvv_opcode_dict.Add(139, "vfredosum.vs");
                rvv_opcode_dict.Add(140, "vfmin.vv");
                rvv_opcode_dict.Add(141, "vfredmin.vs");
                rvv_opcode_dict.Add(142, "vfmax.vv");
                rvv_opcode_dict.Add(143, "vfredmax.vs");
                rvv_opcode_dict.Add(144, "vfsgnj.vv");
                rvv_opcode_dict.Add(145, "vfsgnjn.vv");
                rvv_opcode_dict.Add(146, "vfsgnjx.vv");
                rvv_opcode_dict.Add(147, "vfmv.f.s");
                rvv_opcode_dict.Add(148, "vmfeq.vv");
                rvv_opcode_dict.Add(149, "vmfle.vv");
                rvv_opcode_dict.Add(150, "vmflt.vv");
                rvv_opcode_dict.Add(151, "vmfne.vv");
                rvv_opcode_dict.Add(152, "vfdiv.vv");
                rvv_opcode_dict.Add(153, "vfmul.vv");
                rvv_opcode_dict.Add(154, "vfmadd.vv");
                rvv_opcode_dict.Add(155, "vfnmadd.vv");
                rvv_opcode_dict.Add(156, "vfmsub.vv");
                rvv_opcode_dict.Add(157, "vfnmsub.vv");
                rvv_opcode_dict.Add(158, "vfmacc.vv");
                rvv_opcode_dict.Add(159, "vfnmacc.vv");
                rvv_opcode_dict.Add(160, "vfmsac.vv");
                rvv_opcode_dict.Add(161, "vfnmsac.vv");
                rvv_opcode_dict.Add(162, "vfcvt.xu.f.v");
                rvv_opcode_dict.Add(163, "vfcvt.x.f.v");
                rvv_opcode_dict.Add(164, "vfcvt.f.xu.v");
                rvv_opcode_dict.Add(165, "vfcvt.f.x.v");
                rvv_opcode_dict.Add(166, "vfcvt.rtz.xu.f.v");
                rvv_opcode_dict.Add(167, "vfcvt.rtz.x.f.v");
                rvv_opcode_dict.Add(168, "vfwcvt.xu.f.v");
                rvv_opcode_dict.Add(169, "vfwcvt.x.f.v");
                rvv_opcode_dict.Add(170, "vfwcvt.f.xu.v");
                rvv_opcode_dict.Add(171, "vfwcvt.f.x.v");
                rvv_opcode_dict.Add(172, "vfwcvt.f.f.v");
                rvv_opcode_dict.Add(173, "vfwcvt.rtz.xu.f.v");
                rvv_opcode_dict.Add(174, "vfwcvt.rtz.x.f.v");
                rvv_opcode_dict.Add(175, "vfncvt.xu.f.w");
                rvv_opcode_dict.Add(176, "vfncvt.x.f.w");
                rvv_opcode_dict.Add(177, "vfncvt.f.xu.w");
                rvv_opcode_dict.Add(178, "vfncvt.f.x.w");
                rvv_opcode_dict.Add(179, "vfncvt.f.f.w");
                rvv_opcode_dict.Add(180, "vfncvt.rod.f.f.w");
                rvv_opcode_dict.Add(181, "vfncvt.rtz.xu.f.w");
                rvv_opcode_dict.Add(182, "vfncvt.rtz.x.f.w");
                rvv_opcode_dict.Add(183, "vfsqrt.v");
                rvv_opcode_dict.Add(184, "vfrsqrt7.v");
                rvv_opcode_dict.Add(185, "vfrec7.v");
                rvv_opcode_dict.Add(186, "vfclass.v");
                rvv_opcode_dict.Add(187, "vfwadd.vv");
                rvv_opcode_dict.Add(188, "vfwredusum.vs");
                rvv_opcode_dict.Add(189, "vfwsub.vv");
                rvv_opcode_dict.Add(190, "vfwredosum.vs");
                rvv_opcode_dict.Add(191, "vfwadd.wv");
                rvv_opcode_dict.Add(192, "vfwsub.wv");
                rvv_opcode_dict.Add(193, "vfwmul.vv");
                rvv_opcode_dict.Add(194, "vfwmacc.vv");
                rvv_opcode_dict.Add(195, "vfwnmacc.vv");
                rvv_opcode_dict.Add(196, "vfwmsac.vv");
                rvv_opcode_dict.Add(197, "vfwnmsac.vv");
                rvv_opcode_dict.Add(198, "vadd.vx");
                rvv_opcode_dict.Add(199, "vsub.vx");
                rvv_opcode_dict.Add(200, "vrsub.vx");
                rvv_opcode_dict.Add(201, "vminu.vx");
                rvv_opcode_dict.Add(202, "vmin.vx");
                rvv_opcode_dict.Add(203, "vmaxu.vx");
                rvv_opcode_dict.Add(204, "vmax.vx");
                rvv_opcode_dict.Add(205, "vand.vx");
                rvv_opcode_dict.Add(206, "vor.vx");
                rvv_opcode_dict.Add(207, "vxor.vx");
                rvv_opcode_dict.Add(208, "vrgather.vx");
                rvv_opcode_dict.Add(209, "vslideup.vx");
                rvv_opcode_dict.Add(210, "vslidedown.vx");
                rvv_opcode_dict.Add(211, "vadc.vxm");
                rvv_opcode_dict.Add(212, "vmadc.vxm");
                rvv_opcode_dict.Add(213, "vmadc.vx");
                rvv_opcode_dict.Add(214, "vsbc.vxm");
                rvv_opcode_dict.Add(215, "vmsbc.vxm");
                rvv_opcode_dict.Add(216, "vmsbc.vx");
                rvv_opcode_dict.Add(217, "vmerge.vxm");
                rvv_opcode_dict.Add(218, "vmv.v.x");
                rvv_opcode_dict.Add(219, "vmseq.vx");
                rvv_opcode_dict.Add(220, "vmsne.vx");
                rvv_opcode_dict.Add(221, "vmsltu.vx");
                rvv_opcode_dict.Add(222, "vmslt.vx");
                rvv_opcode_dict.Add(223, "vmsleu.vx");
                rvv_opcode_dict.Add(224, "vmsle.vx");
                rvv_opcode_dict.Add(225, "vmsgtu.vx");
                rvv_opcode_dict.Add(226, "vmsgt.vx");
                rvv_opcode_dict.Add(227, "vsaddu.vx");
                rvv_opcode_dict.Add(228, "vsadd.vx");
                rvv_opcode_dict.Add(229, "vssubu.vx");
                rvv_opcode_dict.Add(230, "vssub.vx");
                rvv_opcode_dict.Add(231, "vsll.vx");
                rvv_opcode_dict.Add(232, "vsmul.vx");
                rvv_opcode_dict.Add(233, "vsrl.vx");
                rvv_opcode_dict.Add(234, "vsra.vx");
                rvv_opcode_dict.Add(235, "vssrl.vx");
                rvv_opcode_dict.Add(236, "vssra.vx");
                rvv_opcode_dict.Add(237, "vnsrl.wx");
                rvv_opcode_dict.Add(238, "vnsra.wx");
                rvv_opcode_dict.Add(239, "vnclipu.wx");
                rvv_opcode_dict.Add(240, "vnclip.wx");
                rvv_opcode_dict.Add(241, "vadd.vv");
                rvv_opcode_dict.Add(242, "vsub.vv");
                rvv_opcode_dict.Add(243, "vminu.vv");
                rvv_opcode_dict.Add(244, "vmin.vv");
                rvv_opcode_dict.Add(245, "vmaxu.vv");
                rvv_opcode_dict.Add(246, "vmax.vv");
                rvv_opcode_dict.Add(247, "vand.vv");
                rvv_opcode_dict.Add(248, "vor.vv");
                rvv_opcode_dict.Add(249, "vxor.vv");
                rvv_opcode_dict.Add(250, "vrgather.vv");
                rvv_opcode_dict.Add(251, "vrgatherei16.vv");
                rvv_opcode_dict.Add(252, "vadc.vvm");
                rvv_opcode_dict.Add(253, "vmadc.vvm");
                rvv_opcode_dict.Add(254, "vmadc.vv");
                rvv_opcode_dict.Add(255, "vsbc.vvm");
                rvv_opcode_dict.Add(256, "vmsbc.vvm");
                rvv_opcode_dict.Add(257, "vmsbc.vv");
                rvv_opcode_dict.Add(258, "vmerge.vvm");
                rvv_opcode_dict.Add(259, "vmv.v.v");
                rvv_opcode_dict.Add(260, "vmseq.vv");
                rvv_opcode_dict.Add(261, "vmsne.vv");
                rvv_opcode_dict.Add(262, "vmsltu.vv");
                rvv_opcode_dict.Add(263, "vmslt.vv");
                rvv_opcode_dict.Add(264, "vmsleu.vv");
                rvv_opcode_dict.Add(265, "vmsle.vv");
                rvv_opcode_dict.Add(266, "vsaddu.vv");
                rvv_opcode_dict.Add(267, "vsadd.vv");
                rvv_opcode_dict.Add(268, "vssubu.vv");
                rvv_opcode_dict.Add(269, "vssub.vv");
                rvv_opcode_dict.Add(270, "vsll.vv");
                rvv_opcode_dict.Add(271, "vsmul.vv");
                rvv_opcode_dict.Add(272, "vsrl.vv");
                rvv_opcode_dict.Add(273, "vsra.vv");
                rvv_opcode_dict.Add(274, "vssrl.vv");
                rvv_opcode_dict.Add(275, "vssra.vv");
                rvv_opcode_dict.Add(276, "vnsrl.wv");
                rvv_opcode_dict.Add(277, "vnsra.wv");
                rvv_opcode_dict.Add(278, "vnclipu.wv");
                rvv_opcode_dict.Add(279, "vnclip.wv");
                rvv_opcode_dict.Add(280, "vwredsumu.vs");
                rvv_opcode_dict.Add(281, "vwredsum.vs");
                rvv_opcode_dict.Add(282, "vadd.vi");
                rvv_opcode_dict.Add(283, "vrsub.vi");
                rvv_opcode_dict.Add(284, "vand.vi");
                rvv_opcode_dict.Add(285, "vor.vi");
                rvv_opcode_dict.Add(286, "vxor.vi");
                rvv_opcode_dict.Add(287, "vrgather.vi");
                rvv_opcode_dict.Add(288, "vslideup.vi");
                rvv_opcode_dict.Add(289, "vslidedown.vi");
                rvv_opcode_dict.Add(290, "vadc.vim");
                rvv_opcode_dict.Add(291, "vmadc.vim");
                rvv_opcode_dict.Add(292, "vmadc.vi");
                rvv_opcode_dict.Add(293, "vmerge.vim");
                rvv_opcode_dict.Add(294, "vmv.v.i");
                rvv_opcode_dict.Add(295, "vmseq.vi");
                rvv_opcode_dict.Add(296, "vmsne.vi");
                rvv_opcode_dict.Add(297, "vmsleu.vi");
                rvv_opcode_dict.Add(298, "vmsle.vi");
                rvv_opcode_dict.Add(299, "vmsgtu.vi");
                rvv_opcode_dict.Add(300, "vmsgt.vi");
                rvv_opcode_dict.Add(301, "vsaddu.vi");
                rvv_opcode_dict.Add(302, "vsadd.vi");
                rvv_opcode_dict.Add(303, "vsll.vi");
                rvv_opcode_dict.Add(304, "vmv1r.v");
                rvv_opcode_dict.Add(305, "vmv2r.v");
                rvv_opcode_dict.Add(306, "vmv4r.v");
                rvv_opcode_dict.Add(307, "vmv8r.v");
                rvv_opcode_dict.Add(308, "vsrl.vi");
                rvv_opcode_dict.Add(309, "vsra.vi");
                rvv_opcode_dict.Add(310, "vssrl.vi");
                rvv_opcode_dict.Add(311, "vssra.vi");
                rvv_opcode_dict.Add(312, "vnsrl.wi");
                rvv_opcode_dict.Add(313, "vnsra.wi");
                rvv_opcode_dict.Add(314, "vnclipu.wi");
                rvv_opcode_dict.Add(315, "vnclip.wi");
                rvv_opcode_dict.Add(316, "vredsum.vs");
                rvv_opcode_dict.Add(317, "vredand.vs");
                rvv_opcode_dict.Add(318, "vredor.vs");
                rvv_opcode_dict.Add(319, "vredxor.vs");
                rvv_opcode_dict.Add(320, "vredminu.vs");
                rvv_opcode_dict.Add(321, "vredmin.vs");
                rvv_opcode_dict.Add(322, "vredmaxu.vs");
                rvv_opcode_dict.Add(323, "vredmax.vs");
                rvv_opcode_dict.Add(324, "vaaddu.vv");
                rvv_opcode_dict.Add(325, "vaadd.vv");
                rvv_opcode_dict.Add(326, "vasubu.vv");
                rvv_opcode_dict.Add(327, "vasub.vv");
                rvv_opcode_dict.Add(328, "vmv.x.s");
                rvv_opcode_dict.Add(329, "vzext.vf8");
                rvv_opcode_dict.Add(330, "vsext.vf8");
                rvv_opcode_dict.Add(331, "vzext.vf4");
                rvv_opcode_dict.Add(332, "vsext.vf4");
                rvv_opcode_dict.Add(333, "vzext.vf2");
                rvv_opcode_dict.Add(334, "vsext.vf2");
                rvv_opcode_dict.Add(335, "vcompress.vm");
                rvv_opcode_dict.Add(336, "vmandnot.mm");
                rvv_opcode_dict.Add(337, "vmand.mm");
                rvv_opcode_dict.Add(338, "vmor.mm");
                rvv_opcode_dict.Add(339, "vmxor.mm");
                rvv_opcode_dict.Add(340, "vmornot.mm");
                rvv_opcode_dict.Add(341, "vmnand.mm");
                rvv_opcode_dict.Add(342, "vmnor.mm");
                rvv_opcode_dict.Add(343, "vmxnor.mm");
                rvv_opcode_dict.Add(344, "vmsbf.m");
                rvv_opcode_dict.Add(345, "vmsof.m");
                rvv_opcode_dict.Add(346, "vmsif.m");
                rvv_opcode_dict.Add(347, "viota.m");
                rvv_opcode_dict.Add(348, "vid.v");
                rvv_opcode_dict.Add(349, "vcpop.m");
                rvv_opcode_dict.Add(350, "vfirst.m");
                rvv_opcode_dict.Add(351, "vdivu.vv");
                rvv_opcode_dict.Add(352, "vdiv.vv");
                rvv_opcode_dict.Add(353, "vremu.vv");
                rvv_opcode_dict.Add(354, "vrem.vv");
                rvv_opcode_dict.Add(355, "vmulhu.vv");
                rvv_opcode_dict.Add(356, "vmul.vv");
                rvv_opcode_dict.Add(357, "vmulhsu.vv");
                rvv_opcode_dict.Add(358, "vmulh.vv");
                rvv_opcode_dict.Add(359, "vmadd.vv");
                rvv_opcode_dict.Add(360, "vnmsub.vv");
                rvv_opcode_dict.Add(361, "vmacc.vv");
                rvv_opcode_dict.Add(362, "vnmsac.vv");
                rvv_opcode_dict.Add(363, "vwaddu.vv");
                rvv_opcode_dict.Add(364, "vwadd.vv");
                rvv_opcode_dict.Add(365, "vwsubu.vv");
                rvv_opcode_dict.Add(366, "vwsub.vv");
                rvv_opcode_dict.Add(367, "vwaddu.wv");
                rvv_opcode_dict.Add(368, "vwadd.wv");
                rvv_opcode_dict.Add(369, "vwsubu.wv");
                rvv_opcode_dict.Add(370, "vwsub.wv");
                rvv_opcode_dict.Add(371, "vwmulu.vv");
                rvv_opcode_dict.Add(372, "vwmulsu.vv");
                rvv_opcode_dict.Add(373, "vwmul.vv");
                rvv_opcode_dict.Add(374, "vwmaccu.vv");
                rvv_opcode_dict.Add(375, "vwmacc.vv");
                rvv_opcode_dict.Add(376, "vwmaccsu.vv");
                rvv_opcode_dict.Add(377, "vaaddu.vx");
                rvv_opcode_dict.Add(378, "vaadd.vx");
                rvv_opcode_dict.Add(379, "vasubu.vx");
                rvv_opcode_dict.Add(380, "vasub.vx");
                rvv_opcode_dict.Add(381, "vmv.s.x");
                rvv_opcode_dict.Add(382, "vslide1up.vx");
                rvv_opcode_dict.Add(383, "vslide1down.vx");
                rvv_opcode_dict.Add(384, "vdivu.vx");
                rvv_opcode_dict.Add(385, "vdiv.vx");
                rvv_opcode_dict.Add(386, "vremu.vx");
                rvv_opcode_dict.Add(387, "vrem.vx");
                rvv_opcode_dict.Add(388, "vmulhu.vx");
                rvv_opcode_dict.Add(389, "vmul.vx");
                rvv_opcode_dict.Add(390, "vmulhsu.vx");
                rvv_opcode_dict.Add(391, "vmulh.vx");
                rvv_opcode_dict.Add(392, "vmadd.vx");
                rvv_opcode_dict.Add(393, "vnmsub.vx");
                rvv_opcode_dict.Add(394, "vmacc.vx");
                rvv_opcode_dict.Add(395, "vnmsac.vx");
                rvv_opcode_dict.Add(396, "vwaddu.vx");
                rvv_opcode_dict.Add(397, "vwadd.vx");
                rvv_opcode_dict.Add(398, "vwsubu.vx");
                rvv_opcode_dict.Add(399, "vwsub.vx");
                rvv_opcode_dict.Add(400, "vwaddu.wx");
                rvv_opcode_dict.Add(401, "vwadd.wx");
                rvv_opcode_dict.Add(402, "vwsubu.wx");
                rvv_opcode_dict.Add(403, "vwsub.wx");
                rvv_opcode_dict.Add(404, "vwmulu.vx");
                rvv_opcode_dict.Add(405, "vwmulsu.vx");
                rvv_opcode_dict.Add(406, "vwmul.vx");
                rvv_opcode_dict.Add(407, "vwmaccu.vx");
                rvv_opcode_dict.Add(408, "vwmacc.vx");
                rvv_opcode_dict.Add(409, "vwmaccus.vx");
                rvv_opcode_dict.Add(410, "vwmaccsu.vx");
                rvv_opcode_dict.Add(411, "vamoswapei8.v");
                rvv_opcode_dict.Add(412, "vamoaddei8.v");
                rvv_opcode_dict.Add(413, "vamoxorei8.v");
                rvv_opcode_dict.Add(414, "vamoandei8.v");
                rvv_opcode_dict.Add(415, "vamoorei8.v");
                rvv_opcode_dict.Add(416, "vamominei8.v");
                rvv_opcode_dict.Add(417, "vamomaxei8.v");
                rvv_opcode_dict.Add(418, "vamominuei8.v");
                rvv_opcode_dict.Add(419, "vamomaxuei8.v");
                rvv_opcode_dict.Add(420, "vamoswapei16.v");
                rvv_opcode_dict.Add(421, "vamoaddei16.v");
                rvv_opcode_dict.Add(422, "vamoxorei16.v");
                rvv_opcode_dict.Add(423, "vamoandei16.v");
                rvv_opcode_dict.Add(424, "vamoorei16.v");
                rvv_opcode_dict.Add(425, "vamominei16.v");
                rvv_opcode_dict.Add(426, "vamomaxei16.v");
                rvv_opcode_dict.Add(427, "vamominuei16.v");
                rvv_opcode_dict.Add(428, "vamomaxuei16.v");
                rvv_opcode_dict.Add(429, "vamoswapei32.v");
                rvv_opcode_dict.Add(430, "vamoaddei32.v");
                rvv_opcode_dict.Add(431, "vamoxorei32.v");
                rvv_opcode_dict.Add(432, "vamoandei32.v");
                rvv_opcode_dict.Add(433, "vamoorei32.v");
                rvv_opcode_dict.Add(434, "vamominei32.v");
                rvv_opcode_dict.Add(435, "vamomaxei32.v");
                rvv_opcode_dict.Add(436, "vamominuei32.v");
                rvv_opcode_dict.Add(437, "vamomaxuei32.v");
                rvv_opcode_dict.Add(438, "vamoswapei64.v");
                rvv_opcode_dict.Add(439, "vamoaddei64.v");
                rvv_opcode_dict.Add(440, "vamoxorei64.v");
                rvv_opcode_dict.Add(441, "vamoandei64.v");
                rvv_opcode_dict.Add(442, "vamoorei64.v");
                rvv_opcode_dict.Add(443, "vamominei64.v");
                rvv_opcode_dict.Add(444, "vamomaxei64.v");
                rvv_opcode_dict.Add(445, "vamominuei64.v");
                rvv_opcode_dict.Add(446, "vamomaxuei64.v");



                for (uint i = 0; i < 447; i++)
                {
                    writer.WriteLine(TlibGetRvvOpcodeCount(i).ToString() + "," + rvv_opcode_dict[i]);
                }
            }
        }

        public bool WfiAsNop
        {
            get => neverWaitForInterrupt;
            set
            {
                neverWaitForInterrupt = value;
            }
        }

        public uint VectorRegisterLength
        {
            set
            {
                if(!SupportsInstructionSet(InstructionSet.V))
                {
                    throw new RecoverableException("Attempted to set Vector Register Length (VLEN), but V extention is not enabled");
                }
                if(TlibSetVlen(value) != 0)
                {
                    throw new RecoverableException($"Attempted to set Vector Register Length (VLEN), but {value} is not a valid value");
                }
            }
        }

        public uint VectorElementMaxWidth
        {
            set
            {
                if(!SupportsInstructionSet(InstructionSet.V))
                {
                    throw new RecoverableException("Attempted to set Vector Element Max Width (ELEN), but V extention is not enabled");
                }
                if(TlibSetElen(value) != 0)
                {
                    throw new RecoverableException($"Attempted to set Vector Element Max Width (ELEN), but {value} is not a valid value");
                }
            }
        }

        public ulong? NMIVectorAddress { get; }

        public uint? NMIVectorLength { get; }

        public event Action<ulong> MipChanged;

        public Dictionary<string, object> UserState { get; }

        public override List<GBDFeatureDescriptor> GDBFeatures
        {
            get
            {
                if(gdbFeatures.Any())
                {
                    return gdbFeatures;
                }

                var registerWidth = (uint)MostSignificantBit + 1;
                RiscVRegisterDescription.AddCpuFeature(ref gdbFeatures, registerWidth);
                RiscVRegisterDescription.AddFpuFeature(ref gdbFeatures, registerWidth, false, SupportsInstructionSet(InstructionSet.F), SupportsInstructionSet(InstructionSet.D), false);
                RiscVRegisterDescription.AddCSRFeature(ref gdbFeatures, registerWidth, SupportsInstructionSet(InstructionSet.S), SupportsInstructionSet(InstructionSet.U), false);
                RiscVRegisterDescription.AddVirtualFeature(ref gdbFeatures, registerWidth);
                RiscVRegisterDescription.AddCustomCSRFeature(ref gdbFeatures, registerWidth, nonstandardCSR);

                return gdbFeatures;
            }
        }

        protected override Interrupt DecodeInterrupt(int number)
        {
            return Interrupt.Hard;
        }

        protected void PCWritten()
        {
            pcWrittenFlag = true;
        }

        protected override string GetExceptionDescription(ulong exceptionIndex)
        {
            var decoded = (exceptionIndex << 1) >> 1;
            var descriptionMap = IsInterrupt(exceptionIndex)
                ? InterruptDescriptionsMap
                : ExceptionDescriptionsMap;

            if(descriptionMap.TryGetValue(decoded, out var result))
            {
                return result;
            }
            return base.GetExceptionDescription(exceptionIndex);
        }

        protected bool TrySetCustomCSR(int register, ulong value)
        {
            if(!nonstandardCSR.ContainsKey((ulong)register))
            {
                return false;
            }
            WriteCSR((ulong)register, value);
            return true;
        }

        protected bool TryGetCustomCSR(int register, out ulong value)
        {
            value = default(ulong);
            if(!nonstandardCSR.ContainsKey((ulong)register))
            {
                return false;
            }
            value = ReadCSR((ulong)register);
            return true;
        }

        protected IEnumerable<CPURegister> GetCustomCSRs()
        {
            return nonstandardCSR.Keys.Select(index => new CPURegister((int)index, MostSignificantBit + 1, false, false));
        }

        private bool IsInterrupt(ulong exceptionIndex)
        {
            return BitHelper.IsBitSet(exceptionIndex, MostSignificantBit);
        }

        protected abstract byte MostSignificantBit { get; }

        private void EnableArchitectureVariants()
        {
            foreach(var @set in architectureSets)
            {
                if(Enum.IsDefined(typeof(InstructionSet), set))
                {
                    TlibAllowFeature((uint)set);
                }
                else if((int)set == 'G' - 'A')
                {
                    //G is a wildcard denoting multiple instruction sets
                    foreach(var gSet in new[] { InstructionSet.I, InstructionSet.M, InstructionSet.F, InstructionSet.D, InstructionSet.A })
                    {
                        TlibAllowFeature((uint)gSet);
                    }
                }
                else
                {
                    this.Log(LogLevel.Warning, $"Undefined instruction set: {char.ToUpper((char)(set + 'A'))}.");
                }
            }
            TlibSetPrivilegeArchitecture((int)privilegeArchitecture);
        }

        private IEnumerable<InstructionSet> DecodeArchitecture(string architecture)
        {
            //The architecture name is: RV{architecture_width}{list of letters denoting instruction sets}
            return architecture.Skip(2).SkipWhile(x => Char.IsDigit(x))
                               .Select(x => (InstructionSet)(Char.ToUpper(x) - 'A'));
        }

        [Export]
        private ulong GetCPUTime()
        {
            if(timeProvider == null)
            {
                this.Log(LogLevel.Warning, "Trying to read time from CPU, but no time provider is registered.");
                return 0;
            }

            SyncTime();
            return timeProvider.TimerValue;
        }

        [Export]
        private ulong ReadCSR(ulong csr)
        {
            var readMethod = nonstandardCSR[csr].ReadOperation;
            if(readMethod == null)
            {
                this.Log(LogLevel.Warning, "Read method is not implemented for CSR=0x{0:X}", csr);
                return 0;
            }
            return readMethod();
        }

        [Export]
        private void WriteCSR(ulong csr, ulong value)
        {
            var writeMethod = nonstandardCSR[csr].WriteOperation;
            if(writeMethod == null)
            {
                this.Log(LogLevel.Warning, "Write method is not implemented for CSR=0x{0:X}", csr);
            }
            else
            {
                writeMethod(value);
            }
        }

        [Export]
        private void TlibMipChanged(ulong value)
        {
            MipChanged?.Invoke(value);
        }

        [Export]
        private int HandleCustomInstruction(UInt64 id, UInt64 opcode)
        {
            if(!customInstructionsMapping.TryGetValue(id, out var handler))
            {
                throw new CpuAbortException($"Unexpected instruction of id {id} and opcode 0x{opcode:X}");
            }

            pcWrittenFlag = false;
            handler(opcode);
            return pcWrittenFlag ? 1 : 0;
        }

        public readonly Dictionary<int, ICFU> ChildCollection;

        private bool pcWrittenFlag;

        private readonly IRiscVTimeProvider timeProvider;

        private readonly PrivilegeArchitecture privilegeArchitecture;

        private readonly Dictionary<ulong, NonstandardCSR> nonstandardCSR;

        private readonly IEnumerable<InstructionSet> architectureSets;

        private readonly Dictionary<ulong, Action<UInt64>> customInstructionsMapping;

        private readonly Dictionary<SimpleCSR, ulong> simpleCSRs = new Dictionary<SimpleCSR, ulong>();

        private List<GBDFeatureDescriptor> gdbFeatures = new List<GBDFeatureDescriptor>();

        // 649:  Field '...' is never assigned to, and will always have its default value null
#pragma warning disable 649
        [Import]
        private ActionUInt32 TlibAllowFeature;

        [Import]
        private FuncUInt32UInt32 TlibIsFeatureEnabled;

        [Import]
        private FuncUInt32UInt32 TlibIsFeatureAllowed;

        [Import(Name="tlib_set_privilege_architecture")]
        private ActionInt32 TlibSetPrivilegeArchitecture;

        [Import]
        private ActionUInt32UInt32 TlibSetMipBit;

        [Import]
        private ActionUInt32 TlibSetHartId;

        [Import]
        private FuncUInt32 TlibGetHartId;

        [Import]
        private FuncUInt32UInt32 TlibGetRvvOpcodeCount;

        [Import]
        private Action TlibEnableRvvOpcodeCount;

        [Import]
        private Action TlibDisableRvvOpcodeCount;

        [Import]
        private FuncUInt32 TlibGetRvvOpcodeCountFlag;

        [Import]
        private FuncUInt64UInt64UInt64UInt64 TlibInstallCustomInstruction;

        [Import(Name="tlib_install_custom_csr")]
        private FuncInt32UInt64 TlibInstallCustomCSR;

        [Import]
        private ActionUInt32UInt32 TlibMarkFeatureSilent;

        [Import]
        private ActionUInt64UInt32 TlibSetNmiVector;

        [Import]
        private ActionInt32Int32 TlibSetNmi;

        [Import]
        private ActionUInt32 TlibSetCsrValidationLevel;

        [Import]
        private FuncUInt32 TlibGetCsrValidationLevel;

        [Import]
        private ActionInt32 TlibAllowUnalignedAccesses;

        [Import]
        private ActionInt32 TlibSetInterruptMode;

        [Import]
        private FuncUInt32UInt32 TlibSetVlen;

        [Import]
        private FuncUInt32UInt32 TlibSetElen;

        [Import]
        private FuncUInt64UInt32UInt32 TlibGetVector;

        [Import]
        private ActionUInt32UInt32UInt64 TlibSetVector;

#pragma warning restore 649

        private readonly Dictionary<ulong, string> InterruptDescriptionsMap = new Dictionary<ulong, string>
        {
            {1, "Supervisor software interrupt"},
            {3, "Machine software interrupt"},
            {5, "Supervisor timer interrupt"},
            {7, "Machine timer interrupt"},
            {9, "Supervisor external interrupt"},
            {11, "Machine external interrupt"}
        };

        private readonly Dictionary<ulong, string> ExceptionDescriptionsMap = new Dictionary<ulong, string>
        {
            {0, "Instruction address misaligned"},
            {1, "Instruction access fault"},
            {2, "Illegal instruction"},
            {3, "Breakpoint"},
            {4, "Load address misaligned"},
            {5, "Load access fault"},
            {6, "Store address misaligned"},
            {7, "Store access fault"},
            {8, "Environment call from U-mode"},
            {9, "Environment call from S-mode"},
            {11, "Environment call from M-mode"},
            {12, "Instruction page fault"},
            {13, "Load page fault"},
            {15, "Store page fault"}
        };

        public enum PrivilegeArchitecture
        {
            Priv1_09,
            Priv1_10,
            Priv1_11
        }

        /* The enabled instruction sets are exposed via a register. Each instruction bit is represented
         * by a single bit, in alphabetical order. E.g. bit 0 represents set 'A', bit 12 represents set 'M' etc.
         */
        public enum InstructionSet
        {
            I = 'I' - 'A',
            M = 'M' - 'A',
            A = 'A' - 'A',
            F = 'F' - 'A',
            D = 'D' - 'A',
            C = 'C' - 'A',
            S = 'S' - 'A',
            U = 'U' - 'A',
            V = 'V' - 'A',
        }

        public enum InterruptMode
        {
            // entries match values
            // in tlib, do not change
            Auto = 0,
            Direct = 1,
            Vectored = 2
        }

        protected RegisterValue BeforeMTVECWrite(RegisterValue value)
        {
            return HandleMTVEC_STVECWrite(value, "MTVEC");
        }

        protected RegisterValue BeforeSTVECWrite(RegisterValue value)
        {
            return HandleMTVEC_STVECWrite(value, "STVEC");
        }

        private RegisterValue HandleMTVEC_STVECWrite(RegisterValue value, string registerName)
        {
            switch(interruptMode)
            {
                case InterruptMode.Direct:
                    if((value.RawValue & 0x3) != 0x0)
                    {
                        var originalValue = value;
                        value = RegisterValue.Create(BitHelper.ReplaceBits(value.RawValue, 0x0, width: 2), value.Bits);
                        this.Log(LogLevel.Warning, "CPU is configured in the Direct interrupt mode, modifying {2} to 0x{0:X} (tried to set 0x{1:X})", value.RawValue, originalValue.RawValue, registerName);
                    }
                    break;

                case InterruptMode.Vectored:
                    if((value.RawValue & 0x3) != 0x1)
                    {
                        var originalValue = value;
                        value = RegisterValue.Create(BitHelper.ReplaceBits(value.RawValue, 0x1, width: 2), value.Bits);
                        this.Log(LogLevel.Warning, "CPU is configured in the Vectored interrupt mode, modifying {2}  to 0x{0:X} (tried to set 0x{1:X})", value.RawValue, originalValue.RawValue, registerName);
                    }
                    break;
            }

            return value;
        }

        /* Since Priv 1.10 all hypervisor interrupts descriptions were changed to 'Reserved'
         * Current state can be found in Table 3.6 of the specification (pg. 37 in version 1.11)
         */
        private static bool IsValidInterruptOnlyInV1_09(int irq)
        {
            return irq == (int)IrqType.HypervisorExternalInterrupt
                || irq == (int)IrqType.HypervisorSoftwareInterrupt
                || irq == (int)IrqType.HypervisorTimerInterrupt;
        }

        /* User-level interrupts support extension (N) is not implemented */
        private static bool IsUniplementedInterrupt(int irq)
        {
            return irq == (int)IrqType.UserExternalInterrupt
                || irq == (int)IrqType.UserSoftwareInterrupt
                || irq == (int)IrqType.UserTimerInterrupt;
        }

        private readonly InterruptMode interruptMode;

        protected enum IrqType
        {
            UserSoftwareInterrupt = 0x0,
            SupervisorSoftwareInterrupt = 0x1,
            HypervisorSoftwareInterrupt = 0x2,
            MachineSoftwareInterrupt = 0x3,
            UserTimerInterrupt = 0x4,
            SupervisorTimerInterrupt = 0x5,
            HypervisorTimerInterrupt = 0x6,
            MachineTimerInterrupt = 0x7,
            UserExternalInterrupt = 0x8,
            SupervisorExternalInterrupt = 0x9,
            HypervisorExternalInterrupt = 0xa,
            MachineExternalInterrupt = 0xb
        }
    }
}
