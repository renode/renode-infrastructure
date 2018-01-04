//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Peripherals.Input;
using System.Collections.Generic;

namespace Antmicro.Renode.Extensions.Analyzers.Video.Events
{
    internal class X11ToKeyScanCodeConverter
    {
        static X11ToKeyScanCodeConverter()
        {
            Instance = new X11ToKeyScanCodeConverter();
        }

        public static X11ToKeyScanCodeConverter Instance { get; private set; }

        public KeyScanCode? GetScanCode(int fromValue)
        {
            KeyScanCode result;
            return ToScanCode.TryGetValue(fromValue, out result) ? (KeyScanCode?)result : null;
        }

        private readonly Dictionary<int, KeyScanCode> ToScanCode = new Dictionary<int, KeyScanCode> {
            { 0x25, KeyScanCode.CtrlL },
            { 0x85, KeyScanCode.WinL },  
            { 0x40, KeyScanCode.AltL },  
            { 0x41, KeyScanCode.Space },  
            { 0x6c, KeyScanCode.AltR },  
            { 0x5c, KeyScanCode.AltR },
            { 0x86, KeyScanCode.WinR }, 
            { 0x87, KeyScanCode.WinMenu },
            { 0x69, KeyScanCode.CtrlR },  

            { 0x32, KeyScanCode.ShiftL  },  
            { 0x34, KeyScanCode.Z },  
            { 0x35, KeyScanCode.X },  
            { 0x36, KeyScanCode.C },  
            { 0x37, KeyScanCode.V },
            { 0x38, KeyScanCode.B }, 
            { 0x39, KeyScanCode.N }, 
            { 0x3a, KeyScanCode.M }, 
            { 0x3b, KeyScanCode.OemPeriod },  
            { 0x3c, KeyScanCode.OemComma }, 
            { 0x3d, KeyScanCode.OemQuestion },   
            { 0x3e, KeyScanCode.ShiftR },  

            { 0x42, KeyScanCode.CapsLock },  
            { 0x26, KeyScanCode.A },
            { 0x27, KeyScanCode.S }, 
            { 0x28, KeyScanCode.D },  
            { 0x29, KeyScanCode.F },  
            { 0x2a, KeyScanCode.G },  
            { 0x2b, KeyScanCode.H },  
            { 0x2c, KeyScanCode.J },
            { 0x2d, KeyScanCode.K },  
            { 0x2e, KeyScanCode.L },
            { 0x2f, KeyScanCode.OemSemicolon }, 
            { 0x30, KeyScanCode.OemQuotes },  
            { 0x24, KeyScanCode.Enter },

            { 0x17, KeyScanCode.Tab }, 
            { 0x18, KeyScanCode.Q }, 
            { 0x19, KeyScanCode.W },
            { 0x1a, KeyScanCode.E },
            { 0x1b, KeyScanCode.R },  
            { 0x1c, KeyScanCode.T },
            { 0x1d, KeyScanCode.Y }, 
            { 0x1e, KeyScanCode.U },  
            { 0x1f, KeyScanCode.I },
            { 0x20, KeyScanCode.O },  
            { 0x21, KeyScanCode.P }, 
            { 0x22, KeyScanCode.OemOpenBrackets },  
            { 0x23, KeyScanCode.OemCloseBrackets },

            { 0x31, KeyScanCode.Tilde }, 
            { 0x0a, KeyScanCode.Number1 },  
            { 0x0b, KeyScanCode.Number2 },  
            { 0x0c, KeyScanCode.Number3 },  
            { 0x0d, KeyScanCode.Number4 },  
            { 0x0e, KeyScanCode.Number5 },  
            { 0x0f, KeyScanCode.Number6 },  
            { 0x10, KeyScanCode.Number7 },  
            { 0x11, KeyScanCode.Number8 },  
            { 0x12, KeyScanCode.Number9 },
            { 0x13, KeyScanCode.Number0 }, 
            { 0x14, KeyScanCode.OemMinus },  
            { 0x15, KeyScanCode.OemPlus },
            { 0x33, KeyScanCode.OemPipe },
            { 0x16, KeyScanCode.BackSpace }, 

            { 0x09, KeyScanCode.Escape },  
            { 0x43, KeyScanCode.F1 },  
            { 0x44, KeyScanCode.F2 },  
            { 0x45, KeyScanCode.F3 },  
            { 0x46, KeyScanCode.F4 },  
            { 0x47, KeyScanCode.F5 },  
            { 0x48, KeyScanCode.F6 },  
            { 0x49, KeyScanCode.F7 },  
            { 0x4a, KeyScanCode.F8 },  
            { 0x4b, KeyScanCode.F9 },  
            { 0x4c, KeyScanCode.F10 }, 
            { 0x5f, KeyScanCode.F11 },
            { 0x60, KeyScanCode.F12 },
            { 0x6b, KeyScanCode.PrtSc }, 
            { 0x4e, KeyScanCode.ScrollLock },  
            { 0x7f, KeyScanCode.Pause },  

            { 0x76, KeyScanCode.Insert },  
            { 0x6e, KeyScanCode.Home },  
            { 0x70, KeyScanCode.PageUp },
            { 0x77, KeyScanCode.Delete },  
            { 0x73, KeyScanCode.End },    
            { 0x75, KeyScanCode.PageDown },  

            { 0x6f, KeyScanCode.Up },  
            { 0x74, KeyScanCode.Down },  
            { 0x71, KeyScanCode.Left },  
            { 0x72, KeyScanCode.Right },

            { 0x4d, KeyScanCode.NumLock },  
            { 0x5a, KeyScanCode.Keypad0 },  
            { 0x57, KeyScanCode.Keypad1 },  
            { 0x58, KeyScanCode.Keypad2 },  
            { 0x59, KeyScanCode.Keypad3 },  
            { 0x53, KeyScanCode.Keypad4 },  
            { 0x54, KeyScanCode.Keypad5 },  
            { 0x55, KeyScanCode.Keypad6 },  
            { 0x4f, KeyScanCode.Keypad7 },  
            { 0x50, KeyScanCode.Keypad8 },  
            { 0x51, KeyScanCode.Keypad9 },
            { 0x6a, KeyScanCode.KeypadDivide },  
            { 0x3f, KeyScanCode.KeypadMultiply },  
            { 0x52, KeyScanCode.KeypadMinus },  
            { 0x56, KeyScanCode.KeypadPlus },  
            { 0x5b, KeyScanCode.KeypadComma },  
            { 0x68, KeyScanCode.KeypadEnter },  
        };
    }
}

