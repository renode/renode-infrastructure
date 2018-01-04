//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.Input
{
    public sealed class PS2ScanCodeTranslator
    {
        public static PS2ScanCodeTranslator Instance { get; private set; }

        static PS2ScanCodeTranslator()
        {
            Instance = new PS2ScanCodeTranslator();
        }

        public int GetCode(KeyScanCode scanCode)
        {
            int result;
            if(!mapping.TryGetValue(scanCode, out result))
            {
                result = 0;
            }
            return result;
        }

        private PS2ScanCodeTranslator() 
        {
            mapping = new Dictionary<KeyScanCode, int>();

            mapping.Add(KeyScanCode.Number1, 0x16);
            mapping.Add(KeyScanCode.Number2, 0x1E);
            mapping.Add(KeyScanCode.Number3, 0x26);
            mapping.Add(KeyScanCode.Number4, 0x25);
            mapping.Add(KeyScanCode.Number5, 0x2E);
            mapping.Add(KeyScanCode.Number6, 0x36);
            mapping.Add(KeyScanCode.Number7, 0x3D);
            mapping.Add(KeyScanCode.Number8, 0x3E);
            mapping.Add(KeyScanCode.Number9, 0x46);
            mapping.Add(KeyScanCode.Number0, 0x45);
            mapping.Add(KeyScanCode.Q, 0x15);
            mapping.Add(KeyScanCode.W, 0x1D);
            mapping.Add(KeyScanCode.E, 0x24);
            mapping.Add(KeyScanCode.R, 0x2D);
            mapping.Add(KeyScanCode.T, 0x2C);
            mapping.Add(KeyScanCode.Y, 0x35);
            mapping.Add(KeyScanCode.U, 0x3C);
            mapping.Add(KeyScanCode.I, 0x43);
            mapping.Add(KeyScanCode.O, 0x44);            
            mapping.Add(KeyScanCode.P, 0x4D);
            mapping.Add(KeyScanCode.A, 0x1C);            
            mapping.Add(KeyScanCode.S, 0x1B);
            mapping.Add(KeyScanCode.D, 0x23);
            mapping.Add(KeyScanCode.F, 0x2B);
            mapping.Add(KeyScanCode.G, 0x34);
            mapping.Add(KeyScanCode.H, 0x33);
            mapping.Add(KeyScanCode.J, 0x3B);            
            mapping.Add(KeyScanCode.K, 0x42);
            mapping.Add(KeyScanCode.L, 0x4B);            
            mapping.Add(KeyScanCode.Z, 0x1A);
            mapping.Add(KeyScanCode.X, 0x22);
            mapping.Add(KeyScanCode.C, 0x21);
            mapping.Add(KeyScanCode.V, 0x2A);            
            mapping.Add(KeyScanCode.B, 0x32);
            mapping.Add(KeyScanCode.N, 0x31);
            mapping.Add(KeyScanCode.M, 0x3A);
            mapping.Add(KeyScanCode.OemMinus, 0x4E);
            mapping.Add(KeyScanCode.OemPlus, 0x55);
            mapping.Add(KeyScanCode.OemOpenBrackets, 0x54);
            mapping.Add(KeyScanCode.OemCloseBrackets, 0x5B);
            mapping.Add(KeyScanCode.BackSpace, 0x66);
            mapping.Add(KeyScanCode.OemPipe, 0x5D);
            mapping.Add(KeyScanCode.OemComma, 0x49);
            mapping.Add(KeyScanCode.OemSemicolon, 0x4C);
            mapping.Add(KeyScanCode.OemQuotes, 0x52);
            mapping.Add(KeyScanCode.OemPeriod, 0x41);
            mapping.Add(KeyScanCode.OemQuestion, 0x4A);
            mapping.Add(KeyScanCode.Tab, 0x0D);
            mapping.Add(KeyScanCode.ShiftL, 0x12);
            mapping.Add(KeyScanCode.CapsLock, 0x58);
            mapping.Add(KeyScanCode.ShiftR, 0x59);
            mapping.Add(KeyScanCode.F1, 0x05);
            mapping.Add(KeyScanCode.F2, 0x06);
            mapping.Add(KeyScanCode.F3, 0x04);
            mapping.Add(KeyScanCode.F4, 0x0C);
            mapping.Add(KeyScanCode.F5, 0x03);
            mapping.Add(KeyScanCode.F6, 0x0B);
            mapping.Add(KeyScanCode.F7, 0x83);
            mapping.Add(KeyScanCode.F8, 0x0A);
            mapping.Add(KeyScanCode.F9, 0x01);
            mapping.Add(KeyScanCode.F10, 0x09);
            mapping.Add(KeyScanCode.F11, 0x78);
            mapping.Add(KeyScanCode.F12, 0x07);
            mapping.Add(KeyScanCode.Pause, 0x777E);
            mapping.Add(KeyScanCode.Insert, 0xE070);
            mapping.Add(KeyScanCode.Delete, 0xE071);
            mapping.Add(KeyScanCode.Left, 0xE06B);
            mapping.Add(KeyScanCode.Right, 0xE074);
            mapping.Add(KeyScanCode.Up, 0xE075);
            mapping.Add(KeyScanCode.Down, 0xE072);
            mapping.Add(KeyScanCode.Keypad1, 0x69);
            mapping.Add(KeyScanCode.Keypad2, 0x72);
            mapping.Add(KeyScanCode.Keypad3, 0x7A);
            mapping.Add(KeyScanCode.Keypad4, 0x6B);
            mapping.Add(KeyScanCode.Keypad5, 0x73);
            mapping.Add(KeyScanCode.Keypad6, 0x74);
            mapping.Add(KeyScanCode.Keypad7, 0x6C);
            mapping.Add(KeyScanCode.Keypad8, 0x75);
            mapping.Add(KeyScanCode.Keypad9, 0x7D);
            mapping.Add(KeyScanCode.Keypad0, 0x70);
      //    mapping.Add(KeyScanCode.KDiv, 0x4A);
            mapping.Add(KeyScanCode.KeypadMultiply, 0x7C);
            mapping.Add(KeyScanCode.KeypadMinus, 0x7B);
            mapping.Add(KeyScanCode.KeypadPlus, 0x79);
            mapping.Add(KeyScanCode.KeypadComma, 0x71);
            mapping.Add(KeyScanCode.NumLock, 0x77);
            mapping.Add(KeyScanCode.Enter, 0x5A);
            mapping.Add(KeyScanCode.KeypadEnter, 0xE01C);
            mapping.Add(KeyScanCode.CtrlL, 0x14);
            mapping.Add(KeyScanCode.CtrlR, 0xE014);
            mapping.Add(KeyScanCode.AltL, 0x11);
            mapping.Add(KeyScanCode.AltR, 0xE011);
            mapping.Add(KeyScanCode.WinL, 0xE01F);
            mapping.Add(KeyScanCode.WinR, 0xE027);
            mapping.Add(KeyScanCode.WinMenu, 0xE02F);
            mapping.Add(KeyScanCode.Escape, 0x76);
            mapping.Add(KeyScanCode.Tilde, 0x0E);
            mapping.Add(KeyScanCode.Space, 0x29);
            mapping.Add(KeyScanCode.Home, 0xE06C);
            mapping.Add(KeyScanCode.End, 0xE069);
            mapping.Add(KeyScanCode.PageUp, 0xE07D);
            mapping.Add(KeyScanCode.PageDown, 0xE07A);
            mapping.Add(KeyScanCode.ScrollLock, 0x7E);
        }

        private readonly Dictionary<KeyScanCode, int> mapping;
    }
}

