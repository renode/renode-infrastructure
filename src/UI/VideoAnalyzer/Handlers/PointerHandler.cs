//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Peripherals.Input;
using Xwt;

namespace Antmicro.Renode.Extensions.Analyzers.Video.Handlers
{
    internal abstract class PointerHandler
    {
        protected PointerHandler(IPointerInput input)
        {
            this.input = input;
        }

        public virtual void Init()
        {
        }

        public virtual void ButtonPressed(int button)
        {
            input.Press(ToMouseButton((PointerButton)button));
        }

        public virtual void ButtonReleased(int button)
        {
            input.Release(ToMouseButton((PointerButton)button));
        }

        public abstract void PointerMoved(int x, int y, int dx, int dy);

        private MouseButton ToMouseButton(PointerButton button)
        {
            switch(button)
            {
            case PointerButton.Left:
                return MouseButton.Left;
            case PointerButton.Right:
                return MouseButton.Right;
            case PointerButton.Middle:
                return MouseButton.Middle;
            }

            return MouseButton.Extra;
        }

        protected IPointerInput input;
    }
}

