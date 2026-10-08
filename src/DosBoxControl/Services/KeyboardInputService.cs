using DosBoxControl.Native;

namespace DosBoxControl.Services;

/// <summary>
/// Sends keystrokes via SendInput, which DOSBox (a DirectInput/raw-input consumer)
/// responds to reliably, unlike PostMessage-based key events.
/// </summary>
public sealed class KeyboardInputService
{
    private static readonly Dictionary<string, ushort> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ENTER"] = 0x0D,
        ["RETURN"] = 0x0D,
        ["ESC"] = 0x1B,
        ["ESCAPE"] = 0x1B,
        ["TAB"] = 0x09,
        ["SPACE"] = 0x20,
        ["BACKSPACE"] = 0x08,
        ["DELETE"] = 0x2E,
        ["DEL"] = 0x2E,
        ["INSERT"] = 0x2D,
        ["HOME"] = 0x24,
        ["END"] = 0x23,
        ["PAGEUP"] = 0x21,
        ["PAGEDOWN"] = 0x22,
        ["UP"] = 0x26,
        ["DOWN"] = 0x28,
        ["LEFT"] = 0x25,
        ["RIGHT"] = 0x27,
        ["CTRL"] = 0x11,
        ["CONTROL"] = 0x11,
        ["ALT"] = 0x12,
        ["SHIFT"] = 0x10,
        ["WIN"] = 0x5B,
        ["CAPSLOCK"] = 0x14,
        ["NUMLOCK"] = 0x90,
        ["SCROLLLOCK"] = 0x91,
        ["PRINTSCREEN"] = 0x2C,
        ["PAUSE"] = 0x13,
    };

    public void SendKeySequence(nint targetWindow, IEnumerable<string> keys, int delayMs)
    {
        WindowFinder.ForceForeground(targetWindow);
        Thread.Sleep(80);

        foreach (var keyToken in keys)
        {
            var parts = keyToken.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var vkCodes = parts.Select(ResolveVirtualKey).ToArray();

            // Press modifiers/keys in order, then release in reverse order (chords like ALT+ENTER).
            foreach (var vk in vkCodes)
            {
                SendKeyEvent(vk, keyUp: false);
            }
            Thread.Sleep(Math.Max(delayMs, 10));
            foreach (var vk in vkCodes.Reverse())
            {
                SendKeyEvent(vk, keyUp: true);
            }

            Thread.Sleep(delayMs);
        }
    }

    public void SendText(nint targetWindow, string text, int delayMs)
    {
        WindowFinder.ForceForeground(targetWindow);
        Thread.Sleep(80);

        nint layout = Win32.GetKeyboardLayout(0);

        foreach (char c in text)
        {
            if (c == '\n' || c == '\r')
            {
                SendKeyEvent(0x0D, keyUp: false);
                SendKeyEvent(0x0D, keyUp: true);
                Thread.Sleep(delayMs);
                continue;
            }

            short scan = Win32.VkKeyScanEx(c, layout);
            if (scan == -1)
            {
                SendUnicodeChar(c);
            }
            else
            {
                byte vk = (byte)(scan & 0xFF);
                byte modifiers = (byte)((scan >> 8) & 0xFF);
                bool shift = (modifiers & 0x1) != 0;
                bool ctrl = (modifiers & 0x2) != 0;
                bool alt = (modifiers & 0x4) != 0;

                if (shift) SendKeyEvent(0x10, keyUp: false);
                if (ctrl) SendKeyEvent(0x11, keyUp: false);
                if (alt) SendKeyEvent(0x12, keyUp: false);

                SendKeyEvent(vk, keyUp: false);
                SendKeyEvent(vk, keyUp: true);

                if (alt) SendKeyEvent(0x12, keyUp: true);
                if (ctrl) SendKeyEvent(0x11, keyUp: true);
                if (shift) SendKeyEvent(0x10, keyUp: true);
            }

            Thread.Sleep(delayMs);
        }
    }

    private static ushort ResolveVirtualKey(string token)
    {
        if (NamedKeys.TryGetValue(token, out var vk))
        {
            return vk;
        }

        if (token.Length == 1)
        {
            char c = char.ToUpperInvariant(token[0]);
            if (c is >= 'A' and <= 'Z')
            {
                return c;
            }
            if (c is >= '0' and <= '9')
            {
                return c;
            }
        }

        if (token.Length is 2 or 3 && (token[0] == 'F' || token[0] == 'f') && int.TryParse(token[1..], out int fn) && fn is >= 1 and <= 24)
        {
            return (ushort)(0x70 + (fn - 1));
        }

        throw new ArgumentException($"Unrecognized key name: '{token}'");
    }

    private static void SendUnicodeChar(char c)
    {
        var input = new Win32.INPUT
        {
            type = Win32.INPUT_KEYBOARD,
            U = new Win32.InputUnion
            {
                ki = new Win32.KEYBDINPUT
                {
                    wVk = 0,
                    wScan = c,
                    dwFlags = Win32.KEYEVENTF_UNICODE,
                    time = 0,
                    dwExtraInfo = 0,
                }
            }
        };
        var up = input;
        up.U.ki.dwFlags |= Win32.KEYEVENTF_KEYUP;

        var batch = new[] { input, up };
        Win32.SendInput((uint)batch.Length, batch, System.Runtime.InteropServices.Marshal.SizeOf<Win32.INPUT>());
    }

    private static void SendKeyEvent(ushort vk, bool keyUp)
    {
        var input = new Win32.INPUT
        {
            type = Win32.INPUT_KEYBOARD,
            U = new Win32.InputUnion
            {
                ki = new Win32.KEYBDINPUT
                {
                    wVk = vk,
                    wScan = 0,
                    dwFlags = keyUp ? Win32.KEYEVENTF_KEYUP : 0,
                    time = 0,
                    dwExtraInfo = 0,
                }
            }
        };

        var batch = new[] { input };
        Win32.SendInput((uint)batch.Length, batch, System.Runtime.InteropServices.Marshal.SizeOf<Win32.INPUT>());
    }
}
