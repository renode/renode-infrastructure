//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Peripherals.Input
{

    public interface IAbsolutePositionPointerInput : IPointerInput
    {
        void MoveTo(int x, int y);
        int MaxX {get;}
        int MaxY {get;}

        //These two almost always should equal zero. If you need to provide a 
        //blind area, take a look at IAbsolutePositionPointerInputWithActiveArea.
        int MinX {get;}
        int MinY {get;}
    }
    
}
