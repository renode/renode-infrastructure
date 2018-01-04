//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Peripherals.Input;

namespace Antmicro.Renode.Extensions.Analyzers.Video.Handlers
{
    internal class RelativePointerHandler : PointerHandler
    {
        public RelativePointerHandler(IRelativePositionPointerInput input) : base(input)
        {
        }

        public override void PointerMoved(int x, int y, int dx, int dy)
        {
            ((IRelativePositionPointerInput)input).MoveBy(dx, dy);
        }
    }
}

