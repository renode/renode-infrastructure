//
// Copyright (c) 2010-2022 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
// Copyright (c) 2022-2024 Chris Pick
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Peripherals.Input;
using System.Collections.Generic;
using Xwt;

namespace Antmicro.Renode.Extensions.Analyzers.Video.Events
{
    internal class OSXToKeyScanCodeConverter
    {
        static OSXToKeyScanCodeConverter()
        {
            Instance = new OSXToKeyScanCodeConverter();
        }

        public static OSXToKeyScanCodeConverter Instance { get; private set; }

        public KeyScanCode? GetScanCode(Key key)
        {
            KeyScanCode result;
            return UntranslatedScanCode.TryGetValue(key, out result) ? (KeyScanCode?)result : null;
        }

        private readonly Dictionary<Key, KeyScanCode> UntranslatedScanCode = new Dictionary<Key, KeyScanCode>
        {
            { Key.AltLeft, KeyScanCode.AltL },
            { Key.MetaLeft, KeyScanCode.WinL },
            { Key.Space, KeyScanCode.Space },
            { Key.AltRight, KeyScanCode.AltR },
            { Key.MetaRight, KeyScanCode.WinR },
            { Key.Menu, KeyScanCode.WinMenu },
            { Key.Z, KeyScanCode.Z },
            { Key.z, KeyScanCode.Z },
            { Key.X, KeyScanCode.X },
            { Key.x, KeyScanCode.X },
            { Key.C, KeyScanCode.C },
            { Key.c, KeyScanCode.C },
            { Key.V, KeyScanCode.V },
            { Key.v, KeyScanCode.V },
            { Key.B, KeyScanCode.B },
            { Key.b, KeyScanCode.B },
            { Key.N, KeyScanCode.N },
            { Key.n, KeyScanCode.N },
            { Key.M, KeyScanCode.M },
            { Key.m, KeyScanCode.M },
            { Key.Period, KeyScanCode.OemPeriod },
            { Key.Comma, KeyScanCode.OemComma },
            { Key.Question, KeyScanCode.OemQuestion },
            { Key.ShiftRight, KeyScanCode.ShiftR },

            { Key.CapsLock, KeyScanCode.CapsLock },
            { Key.A, KeyScanCode.A },
            { Key.a, KeyScanCode.A },
            { Key.S, KeyScanCode.S },
            { Key.s, KeyScanCode.S },
            { Key.D, KeyScanCode.D },
            { Key.d, KeyScanCode.D },
            { Key.F, KeyScanCode.F },
            { Key.f, KeyScanCode.F },
            { Key.G, KeyScanCode.G },
            { Key.g, KeyScanCode.G },
            { Key.H, KeyScanCode.H },
            { Key.h, KeyScanCode.H },
            { Key.J, KeyScanCode.J },
            { Key.j, KeyScanCode.J },
            { Key.K, KeyScanCode.K },
            { Key.k, KeyScanCode.K },
            { Key.L, KeyScanCode.L },
            { Key.l, KeyScanCode.L },
            { Key.Semicolon, KeyScanCode.OemSemicolon },
            { Key.Quote, KeyScanCode.OemQuotes },
            { Key.Return, KeyScanCode.Enter },

            { Key.Tab, KeyScanCode.Tab },
            { Key.Q, KeyScanCode.Q },
            { Key.q, KeyScanCode.Q },
            { Key.W, KeyScanCode.W },
            { Key.w, KeyScanCode.W },
            { Key.E, KeyScanCode.E },
            { Key.e, KeyScanCode.E },
            { Key.R, KeyScanCode.R },
            { Key.r, KeyScanCode.R },
            { Key.T, KeyScanCode.T },
            { Key.t, KeyScanCode.T },
            { Key.Y, KeyScanCode.Y },
            { Key.y, KeyScanCode.Y },
            { Key.U, KeyScanCode.U },
            { Key.u, KeyScanCode.U },
            { Key.I, KeyScanCode.I },
            { Key.i, KeyScanCode.I },
            { Key.O, KeyScanCode.O },
            { Key.o, KeyScanCode.O },
            { Key.P, KeyScanCode.P },
            { Key.p, KeyScanCode.P },
            { Key.OpenCurlyBracket, KeyScanCode.OemOpenBrackets },
            { Key.OpenSquareBracket, KeyScanCode.OemOpenBrackets },
            { Key.CloseCurlyBracket, KeyScanCode.OemCloseBrackets },
            { Key.CloseSquareBracket, KeyScanCode.OemCloseBrackets },

            { Key.Tilde, KeyScanCode.Tilde },
            { Key.BackQuote, KeyScanCode.Tilde },
            { Key.K1, KeyScanCode.Number1 },
            { Key.K2, KeyScanCode.Number2 },
            { Key.K3, KeyScanCode.Number3 },
            { Key.K4, KeyScanCode.Number4 },
            { Key.K5, KeyScanCode.Number5 },
            { Key.K6, KeyScanCode.Number6 },
            { Key.K7, KeyScanCode.Number7 },
            { Key.K8, KeyScanCode.Number8 },
            { Key.K9, KeyScanCode.Number9 },
            { Key.K0, KeyScanCode.Number0 },
            { Key.Minus, KeyScanCode.OemMinus },
            { Key.Underscore, KeyScanCode.OemMinus },
            { Key.Plus, KeyScanCode.OemPlus },
            { Key.Equal, KeyScanCode.OemPlus },
            { Key.Backslash, KeyScanCode.OemPipe },
            { Key.Pipe, KeyScanCode.OemPipe },
            { Key.BackSpace, KeyScanCode.BackSpace },

            { Key.Escape, KeyScanCode.Escape },
            { Key.F1, KeyScanCode.F1 },
            { Key.F2, KeyScanCode.F2 },
            { Key.F3, KeyScanCode.F3 },
            { Key.F4, KeyScanCode.F4 },
            { Key.F5, KeyScanCode.F5 },
            { Key.F6, KeyScanCode.F6 },
            { Key.F7, KeyScanCode.F7 },
            { Key.F8, KeyScanCode.F8 },
            { Key.F9, KeyScanCode.F9 },
            { Key.F10, KeyScanCode.F10 },
            { Key.Print, KeyScanCode.PrtSc },
            { Key.ScrollLock, KeyScanCode.ScrollLock },
            { Key.Pause, KeyScanCode.Pause },

            { Key.Insert, KeyScanCode.Insert },
            { Key.Home, KeyScanCode.Home },
            { Key.PageUp, KeyScanCode.PageUp },
            { Key.Delete, KeyScanCode.Delete },
            { Key.End, KeyScanCode.End },
            { Key.PageDown, KeyScanCode.PageDown },

            { Key.Up, KeyScanCode.Up },
            { Key.Down, KeyScanCode.Down },
            { Key.Left, KeyScanCode.Left },
            { Key.Right, KeyScanCode.Right },

            { Key.NumLock, KeyScanCode.NumLock },
            { Key.NumPad0, KeyScanCode.Keypad0 },
            { Key.NumPad1, KeyScanCode.Keypad1 },
            { Key.NumPad2, KeyScanCode.Keypad2 },
            { Key.NumPad3, KeyScanCode.Keypad3 },
            { Key.NumPad4, KeyScanCode.Keypad4 },
            { Key.NumPad5, KeyScanCode.Keypad5 },
            { Key.NumPad6, KeyScanCode.Keypad6 },
            { Key.NumPad7, KeyScanCode.Keypad7 },
            { Key.NumPad8, KeyScanCode.Keypad8 },
            { Key.NumPad9, KeyScanCode.Keypad9 },
            { Key.NumPadDivide, KeyScanCode.KeypadDivide },
            { Key.NumPadMultiply, KeyScanCode.KeypadMultiply },
            { Key.NumPadSubtract, KeyScanCode.KeypadMinus },
            { Key.NumPadAdd, KeyScanCode.KeypadPlus },
            { Key.NumPadDecimal, KeyScanCode.KeypadComma },
            { Key.NumPadEnter, KeyScanCode.KeypadEnter },
        };
    }
}
