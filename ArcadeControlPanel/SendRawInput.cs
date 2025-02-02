﻿/*
 * == Kiosk Control Panel ==
 * This class handles "raw" synthetic keyboard input.
 * This software is licensed under the Mozilla Public License Version 2.0, the details of which can be found in the file LICENSE
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Threading;

namespace ArcadeControlPanel
{
    class SendRawInput
    {
        // This code based on https://github.com/stevetapley/mame-ai
        // No license was specified so I am assuming the author wanted to share it.

        // This class uses SendInput in a way I don't really understand but MAME does.
        // I tried other implementations of SendInput and they didn't work. I'm not sure exactly what it does different.
        // I think it's using "physical" scancodes instead of "virtual" keycodes, whatever that means.

        const int INPUT_MOUSE = 0;
        const int INPUT_KEYBOARD = 1;
        const int INPUT_HARDWARE = 2;
        const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        const uint KEYEVENTF_KEYUP = 0x0002;
        const uint KEYEVENTF_UNICODE = 0x0004;
        const uint KEYEVENTF_SCANCODE = 0x0008;

        struct INPUT
        {
            public int type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct InputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
            [FieldOffset(0)]
            public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [DllImport("user32.dll")]
        static extern IntPtr GetMessageExtraInfo();

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        // This dictionary maps key names to key info objects
        public static Dictionary<string, KeyInfo> KeyData;




        // Send a single keystroke
        public static void SendKey(ushort scanKey, bool KeyDown, bool KeyUp, int delay = 0)
        {
            KEYBDINPUT keybdi;

            // Special case for using virtual-key for windows keys since
            // it seems the direct scancode method is broken for them
            if (scanKey == (ushort)KeyCodes.WIN || scanKey == (ushort)KeyCodes.RWIN)
            {
                ushort vk_code;

                // assign virtual-key code
                // https://docs.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes
                if (scanKey == (ushort)KeyCodes.WIN)
                {
                    vk_code = 0x5B;
                } else // RWIN
                {
                    vk_code = 0x5C;
                }

                keybdi = new KEYBDINPUT
                {
                    wVk = vk_code,
                    dwFlags = 0
                };
            } else // Direct scancode method
            {
                keybdi = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = scanKey,
                    dwFlags = KEYEVENTF_SCANCODE,
                    dwExtraInfo = IntPtr.Zero,
                };
            }

            
            // Not sure why this is an array; investigate later
            INPUT[] keyPress = new INPUT[]
            {
                new INPUT
                {
                    type = INPUT_KEYBOARD,
                    u = new InputUnion
                    {
                        ki = keybdi
                    }
                },
            };

            if (KeyDown == true)
            {
                // Send key DOWN event
                if (SendInput((uint)keyPress.Length, keyPress, Marshal.SizeOf(typeof(INPUT))) == 0)
                {
                    throw new SendRawInputException("SendInput failed with code: " + Marshal.GetLastWin32Error().ToString());
                }
            }

            // Pause if there's a required delay for e.g. MAME
            if (delay > 0) Thread.Sleep(delay);

            if (KeyUp == true)
            {
                // key UP
                keyPress[0].u.ki.dwFlags |= KEYEVENTF_KEYUP;
                if (SendInput((uint)keyPress.Length, keyPress, Marshal.SizeOf(typeof(INPUT))) == 0)
                {
                    throw new SendRawInputException("SendInput failed with code: " + Marshal.GetLastWin32Error().ToString());
                }
            }
        }

        /// <summary>
        /// Send a keydown followed by a keyup, with an optional delay between the two
        /// </summary>
        /// <param name="key">The keycode to send, from the KeyCodes list</param>
        /// <param name="delay">The delay in milliseconds between the keydown and keyup</param>
        public static void SendKeyPress(KeyCodes key, int delay = 0)
        {
            SendKey(KeyData[key.ToString()].Code, true, true, delay);
        }

        /// <summary>
        /// Send a keydown followed by a keyup, with an optional delay between the two
        /// </summary>
        /// <param name="key">The keycode to send, as a string</param>
        /// <param name="delay">The delay in milliseconds between the keydown and keyup</param>
        public static void SendKeyPress(string key, int delay = 0)
        {
            SendKey(KeyData[key].Code, true, true, delay);
        }

        /// <summary>
        /// Send a keydown followed by a keyup, with an optional delay between the two
        /// </summary>
        /// <param name="key">The keycode to send, from the KeyCodes list</param>
        /// <param name="modifier">The modifier to send, from the KeyCodes list (any key is valid, however)</param>
        /// <param name="delay">The delay in milliseconds between the keydown and keyup</param>
        public static void SendKeyPressMod(string key, string modifier, int delay = 0)
        {
            SendKeyDown(modifier); // Turn modifier on
            SendKeyPress(key, delay); // Send key
            SendKeyUp(modifier); // Turn modifier off
        }

        public static void SendKeyDown(string key)
        {
            SendKey(KeyData[key].Code, true, false);
        }

        public static void SendKeyUp(string key)
        {
            SendKey(KeyData[key].Code, false, true);
        }

        // Custom exception
        public class SendRawInputException : Exception
        {
            public SendRawInputException()
            {
            }

            public SendRawInputException(string message)
                : base(message)
            {
            }

            public SendRawInputException(string message, Exception inner)
                : base(message, inner)
            {
            }
        }

        // Stores info about each key; for convenience the keys current state (for modifiers) is stored here as well
        public class KeyInfo
        {
            public ushort Code; // The scancode
            public bool Modifier;  // Whether this key is a modifier
            public bool State; // Whether this modifier key is currently held down

            public KeyInfo(ushort pCode, bool pModifier, bool pState)
            {
                Code = pCode;
                Modifier = pModifier;
                State = pState;
            }
        }

        // Static constructor that populates the KeyData dictionary because C# has atrocious support for 
        // defining static data at compile time
        static SendRawInput()
        {
            KeyData = new Dictionary<string, KeyInfo>();

            // Walk the KeyCodes enum and copy every value into a KeyInfo object
            // Since the enum is the authoritative data source for key IDs this allows us to programmatically
            // identify keys but also access them dynamically at runtime without having two separate
            // arrays that could fall out of sync
            foreach (KeyCodes code in Enum.GetValues(typeof(KeyCodes))) {
                string name = code.ToString();
                ushort value = (ushort)code;
                KeyData.Add(name, new KeyInfo(value, false, false));
            }

            // Flag every modifier key
            KeyData["CTRL"].Modifier = true;
            KeyData["SHIFT"].Modifier = true;
            KeyData["RSHIFT"].Modifier = true;
            KeyData["RCTRL"].Modifier = true;
            KeyData["WIN"].Modifier = true;
            KeyData["RWIN"].Modifier = true;
            KeyData["ALT"].Modifier = true;

            // This will be deleted later
            YeahWereHere = true;
        }

        // This is just used to "bump" the static constructor; it will be replaced with a 
        // noop function later to do the same thing
        public static bool YeahWereHere;

        /* Physical scancodes for theoretically every key
         * A note about this list:
         * It is sourced from somewhere in the Microsoft SDK, so all these names were originally magic values
         * in the Win32 API. I've changed a significant number of them to things I liked more for this macro language.
         * So be aware of that before you attempt to connect this to win32 functions in any way
         * 
         * Most significantly, all the left keys (LALT, LCTRL, LSHIFT, LWIN) have been renamed without the L because almost
         * everyone intends the L.
         * 
         * <strikethrough>Also, entering "N1" instead of "1" sucks and is hard to read, but enums can't contain numeric keys (ouch!) so
         * I aliased all the digits to ONE, TWO, THREE, etc.</strikethrough>
         * I found out enums only permit single entries for each value.
         * 
         * I think I will get rid of the enum soon.
         */ 
        public enum KeyCodes
        {
            ESCAPE = 0x01,
            N1 = 0x02,
            N2 = 0x03,
            N3 = 0x04,
            N4 = 0x05,
            N5 = 0x06,
            N6 = 0x07,
            N7 = 0x08,
            N8 = 0x09,
            N9 = 0x0A,
            N0 = 0x0B,
            MINUS = 0x0C,    /* - on main keyboard */
            EQUALS = 0x0D,
            BACK = 0x0E,    /* backspace */
            TAB = 0x0F,
            Q = 0x10,
            W = 0x11,
            E = 0x12,
            R = 0x13,
            T = 0x14,
            Y = 0x15,
            U = 0x16,
            I = 0x17,
            O = 0x18,
            P = 0x19,
            LBRACKET = 0x1A,
            RBRACKET = 0x1B,
            RETURN = 0x1C,    /* Enter on main keyboard */
            CTRL = 0x1D,
            A = 0x1E,
            S = 0x1F,
            D = 0x20,
            F = 0x21,
            G = 0x22,
            H = 0x23,
            J = 0x24,
            K = 0x25,
            L = 0x26,
            SEMICOLON = 0x27,
            APOSTROPHE = 0x28,
            GRAVE = 0x29,    /* accent grave */
            SHIFT = 0x2A,
            BACKSLASH = 0x2B,
            Z = 0x2C,
            X = 0x2D,
            C = 0x2E,
            V = 0x2F,
            B = 0x30,
            N = 0x31,
            M = 0x32,
            COMMA = 0x33,
            PERIOD = 0x34,    /* . on main keyboard */
            SLASH = 0x35,    /* / on main keyboard */
            RSHIFT = 0x36,
            MULTIPLY = 0x37,    /* * on numeric keypad */
            ALT = 0x38,    /* left Alt */
            SPACE = 0x39,
            CAPITAL = 0x3A,
            F1 = 0x3B,
            F2 = 0x3C,
            F3 = 0x3D,
            F4 = 0x3E,
            F5 = 0x3F,
            F6 = 0x40,
            F7 = 0x41,
            F8 = 0x42,
            F9 = 0x43,
            F10 = 0x44,
            NUMLOCK = 0x45,
            SCROLL = 0x46,    /* Scroll Lock */
            NUMPAD7 = 0x47,
            NUMPAD8 = 0x48,
            NUMPAD9 = 0x49,
            SUBTRACT = 0x4A,    /* - on numeric keypad */
            NUMPAD4 = 0x4B,
            NUMPAD5 = 0x4C,
            NUMPAD6 = 0x4D,
            ADD = 0x4E,    /* + on numeric keypad */
            NUMPAD1 = 0x4F,
            NUMPAD2 = 0x50,
            NUMPAD3 = 0x51,
            NUMPAD0 = 0x52,
            DECIMAL = 0x53,    /* . on numeric keypad */
            OEM_102 = 0x56,    /* <> or \| on RT 102-key keyboard (Non-U.S.) */
            F11 = 0x57,
            F12 = 0x58,
            F13 = 0x64,    /*                     (NEC PC98) */
            F14 = 0x65,    /*                     (NEC PC98) */
            F15 = 0x66,    /*                     (NEC PC98) */
            KANA = 0x70,    /* (Japanese keyboard)            */
            ABNT_C1 = 0x73,    /* /? on Brazilian keyboard */
            CONVERT = 0x79,    /* (Japanese keyboard)            */
            NOCONVERT = 0x7B,    /* (Japanese keyboard)            */
            YEN = 0x7D,    /* (Japanese keyboard)            */
            ABNT_C2 = 0x7E,    /* Numpad . on Brazilian keyboard */
            NUMPADEQUALS = 0x8D,    /* = on numeric keypad (NEC PC98) */
            PREVTRACK = 0x90,    /* Previous Track (DIK_CIRCUMFLEX on Japanese keyboard) */
            AT = 0x91,    /*                     (NEC PC98) */
            COLON = 0x92,    /*                     (NEC PC98) */
            UNDERLINE = 0x93,    /*                     (NEC PC98) */
            KANJI = 0x94,    /* (Japanese keyboard)            */
            STOP = 0x95,    /*                     (NEC PC98) */
            AX = 0x96,    /*                     (Japan AX) */
            UNLABELED = 0x97,    /*                        (J3100) */
            NEXTTRACK = 0x99,    /* Next Track */
            NUMPADENTER = 0x9C,    /* Enter on numeric keypad */
            RCTRL = 0x9D,
            MUTE = 0xA0,    /* Mute */
            CALCULATOR = 0xA1,    /* Calculator */
            PLAYPAUSE = 0xA2,    /* Play / Pause */
            MEDIASTOP = 0xA4,    /* Media Stop */
            VOLUMEDOWN = 0xAE,    /* Volume - */
            VOLUMEUP = 0xB0,    /* Volume + */
            WEBHOME = 0xB2,    /* Web home */
            NUMPADCOMMA = 0xB3,    /* , on numeric keypad (NEC PC98) */
            DIVIDE = 0xB5,    /* / on numeric keypad */
            SYSRQ = 0xB7,
            RALT = 0xB8,    /* right Alt */
            PAUSE = 0xC5,    /* Pause */
            HOME = 0xC7,    /* Home on arrow keypad */
            UP = 0xC8,    /* UpArrow on arrow keypad */
            PRIOR = 0xC9,    /* PgUp on arrow keypad */
            LEFT = 0xCB,    /* LeftArrow on arrow keypad */
            RIGHT = 0xCD,    /* RightArrow on arrow keypad */
            END = 0xCF,    /* End on arrow keypad */
            DOWN = 0xD0,    /* DownArrow on arrow keypad */
            NEXT = 0xD1,    /* PgDn on arrow keypad */
            INSERT = 0xD2,    /* Insert on arrow keypad */
            DELETE = 0xD3,    /* Delete on arrow keypad */
            WIN = 0xDB,    /* Left Windows key */
            RWIN = 0xDC,    /* Right Windows key */
            APPS = 0xDD,    /* AppMenu key */
            POWER = 0xDE,    /* System Power */
            SLEEP = 0xDF,    /* System Sleep */
            WAKE = 0xE3,    /* System Wake */
            WEBSEARCH = 0xE5,    /* Web Search */
            WEBFAVORITES = 0xE6,    /* Web Favorites */
            WEBREFRESH = 0xE7,    /* Web Refresh */
            WEBSTOP = 0xE8,    /* Web Stop */
            WEBFORWARD = 0xE9,    /* Web Forward */
            WEBBACK = 0xEA,    /* Web Back */
            MYCOMPUTER = 0xEB,    /* My Computer */
            MAIL = 0xEC,    /* Mail */
            MEDIASELECT = 0xED,    /* Media Select */
            NULL_VALUE              /* Used for no-match results on validation */
        }
    }
}
