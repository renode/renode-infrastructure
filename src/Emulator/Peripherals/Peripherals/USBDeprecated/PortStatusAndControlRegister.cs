//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Peripherals.USBDeprecated
{
    public class PortStatusAndControlRegister
    {
        public PortStatusAndControlRegister()
        {
        }

        public PortStatusAndControlRegisterChanges Attach()
        {
            uint oldPortValue = portValue;
            portValue |= CurrentConnectStatus | PortEnabledDisabled | PortEnabledDisabledChange | ConnectStatusChange;
            attached = true;
            return CheckChanges(oldPortValue, portValue);
        }

        public void ResetRise()
        {
            reset = true;
        }

        public bool GetReset()
        {
            return reset;
        }

        public PortStatusAndControlRegisterChanges Enable()
        {
            uint oldPortValue = portValue;
            portValue |= PortEnabledDisabled;
            return CheckChanges(oldPortValue, portValue);
        }

        public void ResetFall()
        {
            portValue &= ~(PortReset); //clear reset bit
            //portValue &= ~(PortPower); //clear power bit
            if(attached)
            {
                portValue |= (CurrentConnectStatus); //set connected bit
                portValue &= ~(ConnectStatusChange); //clear connect change bit
                portValue |= (PortEnabledDisabled); //set enable bit
                portValue &= ~(PortEnabledDisabledChange);
                if(device != null)
                    device.Reset();
            }
            reset = false;
        }

        public uint GetValue()
        {
            if(attached && device != null)
                if(device.GetSpeed() == USBDeviceSpeed.High)
                    portValue |= HighSpeed;
            return portValue;
        }

        public PortStatusAndControlRegisterChanges Detach()
        {
            uint oldPortValue = portValue;
            portValue |= ConnectStatusChange;
            portValue &= (~CurrentConnectStatus) & (~PortEnabledDisabled);
            attached = false;
            return CheckChanges(oldPortValue, portValue);
        }

        public PortStatusAndControlRegisterChanges Attach(IUSBPeripheral portDevice)
        {
            uint oldPortValue = portValue;
            portValue |= CurrentConnectStatus | PortEnabledDisabled | PortEnabledDisabledChange | ConnectStatusChange;
            device = portDevice;
            attached = true;
            return CheckChanges(oldPortValue, portValue);
        }

        public PortStatusAndControlRegisterChanges PowerUp()
        {
            uint oldPortValue = portValue;
            //  portValue |= PortEnabledDisabled | CurrentConnectStatus | PortPower; //TODO: Port Power bit should be dependent on PPC
            //portValue |= PortEnabledDisabled | PortPower;
            //powered = true;
            if(attached)
            {
                portValue |= (CurrentConnectStatus); //set connected bit
                portValue |= (ConnectStatusChange); //clear connect change bit
            }
            return CheckChanges(oldPortValue, portValue);
        }

        public PortStatusAndControlRegisterChanges SetValue(uint value)
        {
            PortStatusAndControlRegisterChanges retVal = new PortStatusAndControlRegisterChanges(); //idicates if interrupt should be rised after this fcn
            retVal.ConnectChange = false;
            retVal.EnableChange = false;
            //uint oldValue = portValue;
            uint tmpValue = portValue & ~(WriteMask);
            //if(SystemBus != null) this.Log(LogType.Error,"current PC {0:x}", ((IControllableCPU)SystemBus.GetCPUs().First()).PC);
            portValue = (value & WriteMask) | tmpValue;
            if((value & ConnectStatusChange) != 0)
            {
                portValue &= ~(ConnectStatusChange);
            }
            if((value & PortEnabledDisabledChange) != 0)
            {
                portValue &= ~(PortEnabledDisabledChange);
            }
            if((value & PortPower) != 0 && (powered == false))
            {
                retVal = this.PowerUp();
            }
            if((value & PortReset) != 0)
            {
                this.ResetRise();
                // this.resetFall();
            }
            if(((value & PortReset) == 0) && reset == true)
            {
                ResetFall();
                //retVal.ConnectChange = true;
            }
            if((value & PortEnabledDisabled) != 0)
            {
                retVal = this.Enable();
            }
            if((portValue & PortEnabledDisabled) == 0)
            {
                // if(SystemBus != null)
                //   this.Log(LogType.Error,"zerowanie Enable current PC {0:x}", ((IControllableCPU)SystemBus.GetCPUs().First()).PC);
            }
            /* Remove reset bit */
            portValue &= ~(0x1000u);
            return retVal;
        }

        public const uint CurrentConnectStatus = 1 << 0;
        public const uint ConnectStatusChange = 1 << 1;
        public const uint PortEnabledDisabled = 1 << 2;
        public const uint PortEnabledDisabledChange = 1 << 3;
        public const uint PortReset = 1 << 8;
        public const uint PortPower = 1 << 12;
        public const uint HighSpeed = 1 << 27;
        //FIXME: correct
        public const uint WriteMask = 0x007FE1CC;
        protected IUSBPeripheral device;

        private PortStatusAndControlRegisterChanges CheckChanges(uint oldPortVal, uint newPortVal)
        {
            var change = new PortStatusAndControlRegisterChanges();
            change.ConnectChange = false;
            change.EnableChange = false;
            if((oldPortVal & CurrentConnectStatus) != (newPortVal & CurrentConnectStatus))
            {
                change.ConnectChange = true;
                portValue |= ConnectStatusChange;
            }
            if((oldPortVal & PortEnabledDisabled) != (newPortVal & PortEnabledDisabled))
            {
                change.EnableChange = true;
                portValue |= PortEnabledDisabledChange;
            }
            return change;
        }

        private uint portValue;
        private bool attached = false;

        private bool reset = false;
        private readonly bool powered = false;
    }
}