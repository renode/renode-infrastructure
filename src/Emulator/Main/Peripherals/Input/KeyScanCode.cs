//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.Input
{
    // Here values are according to the USB keyboard scan codes
    public enum KeyScanCode
    {
        NoKey = -1,
        Number1 = 0x1E,
        Number2 = 0x1F,
        Number3 = 0x20,
        Number4 = 0x21,
        Number5 = 0x22,
        Number6 = 0x23,
        Number7 = 0x24,
        Number8 = 0x25,
        Number9 = 0x26,
        Number0 = 0x27,
        Keypad1 = 0x59,
        Keypad2 = 0x5A,
        Keypad3 = 0x5B,
        Keypad4 = 0x5C,
        Keypad5 = 0x5D,
        Keypad6 = 0x5E,
        Keypad7 = 0x5F,
        Keypad8 = 0x60,
        Keypad9 = 0x61,
        Keypad0 = 0x62,
        KeypadMinus = 0x56,
        KeypadPlus = 0x57,
        KeypadMultiply = 0x55,
        KeypadDivide = 0x54,
        KeypadEnter = 0x58,
        KeypadComma = 0x63,
        WinL = 0xE3,
        WinR = 0xE7,
        WinMenu = 0x65,
        Q = 0x14,
        W = 0x1A,
        E = 0x08,
        R = 0x15,
        T = 0x17,
        Y = 0x1C,
        U = 0x18,
        I = 0x0C,
        O = 0x12,
        P = 0x13,
        A = 0x04,
        S = 0x16,
        D = 0x07,
        F = 0x09,
        G = 0x0A,
        H = 0x0B,
        J = 0x0D,
        K = 0x0E,
        L = 0x0F,
        ShiftL = 0xE1,
        Z = 0x1D,
        X = 0x1B,
        C = 0x06,
        V = 0x19,
        B = 0x05,
        N = 0x11,
        M = 0x10,
        ShiftR = 0xE5,
        Enter = 0x28,
        Escape = 0x29,
        BackSpace = 0x2A,
        Tab = 0x2B,
        Space = 0x2C,
        OemMinus = 0x2D,
        OemPlus = 0x2E,
        OemOpenBrackets = 0x2F,
        OemCloseBrackets = 0x30,
        OemPipe = 0x31,
        OemSemicolon = 0x33,
        OemQuotes = 0x34,
        CapsLock = 0x39,
        OemPeriod= 0x36,
        OemComma = 0x37,
        OemQuestion = 0x38,
        NumLock = 0x53,
        Pause = 0x48,
        PrtSc = 0x46,
        ScrollLock = 0x47,
        Insert = 0x49,
        Delete = 0x4C,
        Left = 0x50,
        Right = 0x4F,
        Up = 0x52,
        Down = 0x51,
        CtrlL = 0xE0,
        CtrlR = 0xE4,
        AltL = 0xE2,
        AltR = 0xE6,
        Tilde = 0x35,
        F1 = 0x3A,
        F2 = 0x3B,
        F3 = 0x3C,
        F4 = 0x3D,
        F5 = 0x3E,
        F6 = 0x3F,
        F7 = 0x40,
        F8 = 0x41,
        F9 = 0x42,
        F10 = 0x43,
        F11 = 0x44,
        F12 = 0x45,
        Home = 0x4A,
        End = 0x4D,
        PageUp = 0x4B,
        PageDown = 0x4E
    }

    public static class KeyScanCodeExtensions
    {
        public static KeyScanCode[] ToKeyScanCodes(this char c)
        {
            if(c >= 'a' && c <= 'z')
            {
                return new[] { KeyScanCode.A + (c - 'a') };
            }

            if(c >= 'A' && c <= 'Z')
            {
                return new[] { KeyScanCode.ShiftL, KeyScanCode.A + (c - 'A') };
            }

            // `0` is handled in the switch below as Number0 is after Number9
            // and in ACII 0 is before 1
            if(c >= '1' && c <= '9')
            {
                return new [] { KeyScanCode.Number1 + (c - '1') };
            }

            switch(c)
            {
                case '0':
                    return new [] { KeyScanCode.Number0 };
                case ' ':
                    return new [] { KeyScanCode.Space };
                case '`':
                    return new [] { KeyScanCode.Tilde };
                case '~':
                    return new [] { KeyScanCode.ShiftL, KeyScanCode.Tilde };
                case '!':
                    return new [] { KeyScanCode.ShiftL, KeyScanCode.Number1 };
                case '@':
                    return new [] { KeyScanCode.ShiftL, KeyScanCode.Number2 };
                case '#':
                    return new [] { KeyScanCode.ShiftL, KeyScanCode.Number3 };
                case '$':
                    return new [] { KeyScanCode.ShiftL, KeyScanCode.Number4 };
                case '%':
                    return new [] { KeyScanCode.ShiftL, KeyScanCode.Number5 };
                case '^':
                    return new [] { KeyScanCode.ShiftL, KeyScanCode.Number6 };
                case '&':
                    return new [] { KeyScanCode.ShiftL, KeyScanCode.Number7 };
                case '*':
                    return new [] { KeyScanCode.ShiftL, KeyScanCode.Number8 };
                case '(':
                    return new [] { KeyScanCode.ShiftL, KeyScanCode.Number9 };
                case ')':
                    return new [] { KeyScanCode.ShiftL, KeyScanCode.Number0 };
                case '-':
                    return new [] { KeyScanCode.OemMinus };
                case '_':
                    return new [] { KeyScanCode.ShiftL, KeyScanCode.OemMinus };
                case '=':
                    return new [] { KeyScanCode.OemPlus };
                case '+':
                    return new [] { KeyScanCode.ShiftL, KeyScanCode.OemPlus };
                case '[':
                    return new [] { KeyScanCode.OemOpenBrackets };
                case '{':
                    return new [] { KeyScanCode.ShiftL, KeyScanCode.OemOpenBrackets };
                case ']':
                    return new [] { KeyScanCode.OemCloseBrackets };
                case '}':
                    return new [] { KeyScanCode.ShiftL, KeyScanCode.OemCloseBrackets };
                case '\\':
                    return new [] { KeyScanCode.OemPipe };
                case '|':
                    return new [] { KeyScanCode.ShiftL, KeyScanCode.OemPipe };
                case ';':
                    return new [] { KeyScanCode.OemSemicolon };
                case ':':
                    return new [] { KeyScanCode.ShiftL, KeyScanCode.OemSemicolon };
                case '\'':
                    return new [] { KeyScanCode.OemQuotes };
                case '"':
                    return new [] { KeyScanCode.ShiftL, KeyScanCode.OemQuotes };
                case ',':
                    return new [] { KeyScanCode.OemComma };
                case '<':
                    return new [] { KeyScanCode.ShiftL, KeyScanCode.OemComma };
                case '.':
                    return new [] { KeyScanCode.OemPeriod };
                case '>':
                    return new [] { KeyScanCode.ShiftL, KeyScanCode.OemPeriod };
                case '/':
                    return new [] { KeyScanCode.OemQuestion };
                case '?':
                    return new [] { KeyScanCode.ShiftL, KeyScanCode.OemQuestion };
            }

            return new KeyScanCode[0];
        }
    }
}


