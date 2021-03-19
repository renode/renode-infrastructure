//
// Copyright (c) 2010-2020 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.IO;

namespace Antmicro.Renode.HostInterfaces.Network
{
    public interface ILinuxHostConfig
    {
        void Apply(StreamWriter shellFile);
        void Rewoke(StreamWriter shellFile);
    }
}
