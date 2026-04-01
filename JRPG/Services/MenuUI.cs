using System;
using System.Collections.Generic;

namespace JRPGPrototype.Services
{
    /// <summary>
    /// Static utility for rendering high-fidelity interactive menus.
    /// Refactored to utilize IGameIO instead of direct System.Console calls.
    /// </summary>
    public static class MenuUI
    {
        /// <summary>
        /// Master menu rendering logic.
        /// Signature now accepts 'supportStatusInspect' to handle the [S] key safely.
        /// </summary>
        public static int RenderMenu(IGameIO io, string header, List<string> options, int initialIndex = 0, List<bool>? disabledOptions = null, Action<int>? onHighlight = null, bool supportStatusInspect = false)
        {
            int selectedIndex = initialIndex;
            if (selectedIndex < 0) selectedIndex = 0;
            if (options.Count > 0 && selectedIndex >= options.Count) selectedIndex = 0;

            // Use the IO abstraction to manage cursor state
            io.SetCursorVisible(false);

            while (true)
            {
                io.Clear();
                io.WriteLine(header);

                for (int i = 0; i < options.Count; i++)
                {
                    bool isDisabled = disabledOptions != null && i < disabledOptions.Count && disabledOptions[i];
                    string prefix = (i == selectedIndex) ? "> " : "  ";

                    if (i == selectedIndex)
                    {
                        // Use abstraction for background/foreground swaps
                        io.SetBackgroundColor(ConsoleColor.Gray);
                        io.SetForegroundColor(ConsoleColor.Black);
                        io.WriteLine($"{prefix}{options[i]}");
                        io.ResetColor();
                    }
                    else
                    {
                        if (isDisabled)
                        {
                            io.WriteLine($"{prefix}{options[i]}", ConsoleColor.DarkGray);
                        }
                        else
                        {
                            io.WriteLine($"{prefix}{options[i]}");
                        }
                    }
                }

                // Handle live-reactive highlights (e.g., skill descriptions or stat differentials)
                if (onHighlight != null && options.Count > 0)
                {
                    io.WriteLine("\n------------------------------");

                    // Only show the Inspect hint if the menu context allows it
                    if (supportStatusInspect)
                    {
                        io.WriteLine("[S] View Status | [Enter] Confirm", ConsoleColor.Cyan);
                    }

                    onHighlight(selectedIndex);
                }

                ConsoleKeyInfo keyInfo = io.ReadKey(true);

                if (keyInfo.Key == ConsoleKey.UpArrow)
                {
                    selectedIndex--;
                    if (selectedIndex < 0) selectedIndex = options.Count - 1;
                }
                else if (keyInfo.Key == ConsoleKey.DownArrow)
                {
                    selectedIndex++;
                    if (selectedIndex >= options.Count) selectedIndex = 0;
                }
                else if (keyInfo.Key == ConsoleKey.Enter)
                {
                    bool isDisabled = disabledOptions != null && selectedIndex < disabledOptions.Count && disabledOptions[selectedIndex];
                    if (!isDisabled)
                    {
                        io.SetCursorVisible(true);
                        return selectedIndex;
                    }
                }
                else if (keyInfo.Key == ConsoleKey.S && supportStatusInspect)
                {
                    // Return signal for status peek: -(index + 10)
                    // This is only triggered if explicitly enabled by the caller (InteractionBridge)
                    io.SetCursorVisible(true);
                    return -(selectedIndex + 10);
                }
                else if (keyInfo.Key == ConsoleKey.Escape || keyInfo.Key == ConsoleKey.Backspace)
                {
                    io.SetCursorVisible(true);
                    return -1;
                }
            }
        }
    }
}