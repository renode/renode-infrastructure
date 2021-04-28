//
// Copyright (c) 2010-2021 Antmicro
// Copyright (c) 2021 Sean "xobs" Cross
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Peripherals.Input;
using System.Collections.Generic;
using Xwt;

namespace Antmicro.Renode.Extensions.Analyzers.Video.Events
{
    internal class WPFToKeyScanCodeConverter
    {
        static WPFToKeyScanCodeConverter()
        {
            Instance = new WPFToKeyScanCodeConverter();
        }

        public static WPFToKeyScanCodeConverter Instance { get; private set; }

        public KeyScanCode? GetScanCode(int fromValue, Key untranslatedCode)
        {
            KeyScanCode result;

            // This value shows up for both Return and Minus, so use the
            // untranslated code.
            if(ScanCodesToIgnore.TryGetValue(untranslatedCode, out result))
            {
                return result;
            }
            if(fromValue == 0)
            {
                return UntranslatedScanCode.TryGetValue(untranslatedCode, out result) ? (KeyScanCode?)result : null;
            }
            return ToScanCode.TryGetValue(fromValue, out result) ? (KeyScanCode?)result : null;
        }

        // These all share a scancode with a letter key, so don't bother
        // trying to read the FromValue
        private readonly Dictionary<Key, KeyScanCode> ScanCodesToIgnore = new Dictionary<Key, KeyScanCode>
        {
            { Key.ShiftLeft /*53*/, KeyScanCode.ShiftL },
            { Key.ControlLeft /*53*/, KeyScanCode.CtrlL },
            { Key.ShiftRight /*53*/, KeyScanCode.ShiftR  },
            { Key.ControlRight /*53*/, KeyScanCode.CtrlR },

            { Key.Equal /*53*/, KeyScanCode.OemPlus },
            { Key.Slash /*53*/, KeyScanCode.OemQuestion },

            { Key.BackSpace /*10*/, KeyScanCode.BackSpace },
            { Key.Tab /*11*/, KeyScanCode.Tab },
            { Key.Return /*12*/, KeyScanCode.Enter },
            { Key.CapsLock /*53*/, KeyScanCode.CapsLock },

            { Key.Escape /*39*/, KeyScanCode.Escape },
            { Key.ScrollLock /*5*/, KeyScanCode.ScrollLock },
            { Key.Pause /*4*/, KeyScanCode.Pause },

            { Key.Home /*25*/, KeyScanCode.Home },
            { Key.PageUp /*22*/, KeyScanCode.PageUp },
            { Key.End /*17*/, KeyScanCode.End },
            { Key.PageDown /*47*/, KeyScanCode.PageDown },
            { Key.Insert /*53*/, KeyScanCode.Insert },
            { Key.Delete /*53*/, KeyScanCode.Delete },

            { Key.Up /*19*/, KeyScanCode.Up },
            { Key.Down /*20*/, KeyScanCode.Down },
            { Key.Left /*16*/, KeyScanCode.Left },
            { Key.Right /*31*/, KeyScanCode.Right },

            { Key.NumLock /*53*/, KeyScanCode.NumLock },
            { Key.NumPad0 /*53*/, KeyScanCode.Keypad0 },
            { Key.NumPad1 /*53*/, KeyScanCode.Keypad1 },
            { Key.NumPad2 /*53*/, KeyScanCode.Keypad2 },
            { Key.NumPad3 /*53*/, KeyScanCode.Keypad3 },
            { Key.NumPad4 /*53*/, KeyScanCode.Keypad4 },
            { Key.NumPad5 /*53*/, KeyScanCode.Keypad5 },
            { Key.NumPad6 /*53*/, KeyScanCode.Keypad6 },
            { Key.NumPad7 /*53*/, KeyScanCode.Keypad7 },
            { Key.NumPad8 /*53*/, KeyScanCode.Keypad8 },
            { Key.NumPad9 /*53*/, KeyScanCode.Keypad9 },
            { Key.NumPadDivide /*53*/, KeyScanCode.KeypadDivide },
            { Key.NumPadMultiply /*53*/, KeyScanCode.KeypadMultiply },
            { Key.NumPadSubtract /*53*/, KeyScanCode.KeypadMinus },
            { Key.NumPadAdd /*53*/, KeyScanCode.KeypadPlus },
            { Key.NumPadDecimal /*53*/, KeyScanCode.KeypadComma },
            { Key.NumPadEnter /*12*/, KeyScanCode.KeypadEnter },
            { Key.Space /*57*/, KeyScanCode.Space },

            { Key.F1 /*53*/, KeyScanCode.F1 },
            { Key.F2 /*53*/, KeyScanCode.F2 },
            { Key.F3 /*53*/, KeyScanCode.F3 },
            { Key.F4 /*53*/, KeyScanCode.F4 },
            { Key.F5 /*53*/, KeyScanCode.F5 },
            { Key.F6 /*53*/, KeyScanCode.F6 },
            { Key.F7 /*53*/, KeyScanCode.F7 },
            { Key.F8 /*53*/, KeyScanCode.F8 },
            { Key.F9 /*53*/, KeyScanCode.F9 },
            { Key.F10 /*53*/, KeyScanCode.F10 },
        };

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

        private readonly Dictionary<int, KeyScanCode> ToScanCode = new Dictionary<int, KeyScanCode>
        {
            { 2, KeyScanCode.Number1 },
            { 3, KeyScanCode.Number2 },
            { 4, KeyScanCode.Number3 },
            { 5, KeyScanCode.Number4 },
            { 6, KeyScanCode.Number5 },
            { 7, KeyScanCode.Number6 },
            { 8, KeyScanCode.Number7 },
            { 9, KeyScanCode.Number8 },
            { 10, KeyScanCode.Number9 },
            { 11, KeyScanCode.Number0 },
            { 12, KeyScanCode.OemMinus },
            { 13, KeyScanCode.OemPlus },

            { 16, KeyScanCode.Q },
            { 17, KeyScanCode.W },
            { 18, KeyScanCode.E },
            { 19, KeyScanCode.R },
            { 20, KeyScanCode.T },
            { 21, KeyScanCode.Y },
            { 22, KeyScanCode.U },
            { 23, KeyScanCode.I },
            { 24, KeyScanCode.O },
            { 25, KeyScanCode.P },
            { 26, KeyScanCode.OemOpenBrackets },
            { 27, KeyScanCode.OemCloseBrackets },

            { 30, KeyScanCode.A },
            { 31, KeyScanCode.S },
            { 32, KeyScanCode.D },
            { 33, KeyScanCode.F },
            { 34, KeyScanCode.G },
            { 35, KeyScanCode.H },
            { 36, KeyScanCode.J },
            { 37, KeyScanCode.K },
            { 38, KeyScanCode.L },
            { 39, KeyScanCode.OemSemicolon },
            { 40, KeyScanCode.OemQuotes },

            { 41, KeyScanCode.Tilde },

            { 43, KeyScanCode.OemPipe },

            { 44, KeyScanCode.Z },
            { 45, KeyScanCode.X },
            { 46, KeyScanCode.C },
            { 47, KeyScanCode.V },
            { 48, KeyScanCode.B },
            { 49, KeyScanCode.N },
            { 50, KeyScanCode.M },
            { 51, KeyScanCode.OemComma },
            { 52, KeyScanCode.OemPeriod },
            { 53, KeyScanCode.OemQuestion },

            { 57, KeyScanCode.BackSpace },
        };
    }
}

