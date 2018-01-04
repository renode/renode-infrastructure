//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Network;

namespace Antmicro.Renode.Peripherals.Network
{
    public class NetworkLink
    {
        public NetworkLink(IMACInterface parent)
        {
            this.Parent = parent;
        }

        /// <summary>
        /// Is invoked by a connected endpoint to transmit a frame to the parent interface.
        /// </summary>
        /// <param name="frame">
        /// Network frame.
        /// </param> 
        public void ReceiveFrameOnInterface(EthernetFrame frame)
        {
            SendFrameToInterface(frame);
        }

        /// <summary>
        /// Is invoked by the parent interface to transmit a frame to any connected endpoint.
        /// </summary>
        /// <param name='frame'>
        /// Network frame.
        /// </param>
        public void TransmitFrameFromInterface(EthernetFrame frame)
        {
            var transmit = TransmitFromParentInterface;
            if(transmit != null)
            {
                transmit(this, frame);
            }
        }

        private void SendFrameToInterface(EthernetFrame frame)
        {
            var receive = ReceiveOnParentInterface;
            if(receive != null)
            {
                receive(this, frame);
            }
            Parent.ReceiveFrame(frame);
        }

        /// <summary>
        /// Is invoked when frame is transmitted from the parent interface to any connected endpoint.
        /// </summary>
        public event Action<NetworkLink, EthernetFrame> TransmitFromParentInterface;

        /// <summary>
        /// Is invoked when frame is transmitted from a connected endpoint to the parent interface.
        /// </summary>
        public event Action<NetworkLink, EthernetFrame> ReceiveOnParentInterface;

        /// <summary>
        /// Returns <c>true</c> if there is any receiver for TransmitFromEmulatorToHost event.
        /// </summary>
        /// <value><c>true</c> if this instance is connected; otherwise, <c>false</c>.</value>
        public bool IsConnected
        {
            get{ return TransmitFromParentInterface != null; }
        }

        public IMACInterface Parent { get; private set; }
    }
}

