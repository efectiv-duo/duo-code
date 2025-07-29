using System;
using System.Collections.Generic;
using System.Linq;
using duo_code.Commands.Core;
using duo_code.Models;
using Spectre.Console;

namespace duo_code.Services
{
    public class CustomTextInput
    {
        private readonly CommandRegistry? _commandRegistry;
        private readonly List<string> _commandHistory;
        private readonly Mode _currentMode;
        private int _historyIndex = -1;

        public CustomTextInput(CommandRegistry? commandRegistry, List<string> commandHistory, Mode currentMode)
        {
            _commandRegistry = commandRegistry;
            _commandHistory = commandHistory;
            _currentMode = currentMode;
        }

        public (string input, Mode? modeSwitch) ReadInput()
        {
            // Create a styled input box
            var panel = new Panel("")
            {
                Header = new PanelHeader(GetModeHeader()),
                Border = BoxBorder.Rounded,
                BorderStyle = Style.Parse(GetModeColor()),
                Padding = new Padding(1, 0, 1, 0)
            };

            // Show the input box frame
            var inputArea = new Layout("input")
                .SplitRows(
                    new Layout("prompt").Size(3),
                    new Layout("content").Size(1)
                );

            // Show mode-specific styling with emoji indicators
            var modeEmoji = _currentMode switch
            {
                Mode.Default => "🤖",
                Mode.Planning => "📋",
                Mode.Orchestrator => "🎯",
                _ => "❓"
            };
            
            AnsiConsole.Write(new Rule($"[bold {GetModeColor()}]{modeEmoji} {_currentMode.ToDisplayString()} Mode[/]")
            {
                Style = Style.Parse(GetModeColor())
            });

            // Custom input handling with enhanced visuals
            var line = "";
            var cursorPosition = 0;
            _historyIndex = -1;

            // Show input prompt with styling
            var promptText = $"[{GetModeColor()}]> [/]";
            AnsiConsole.Markup(promptText);
            var promptLength = "> ".Length;

            while (true)
            {
                var keyInfo = Console.ReadKey(true);

                // Check for Shift+Tab (mode switching)
                if (keyInfo.Key == ConsoleKey.Tab && keyInfo.Modifiers == ConsoleModifiers.Shift)
                {
                    Console.WriteLine();
                    return ("SWITCH_MODE", null);
                }

                switch (keyInfo.Key)
                {
                    case ConsoleKey.Enter:
                        Console.WriteLine();
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            AddToHistory(line);
                        }
                        return (line, null);

                    case ConsoleKey.Backspace:
                        if (cursorPosition > 0)
                        {
                            line = line.Remove(cursorPosition - 1, 1);
                            cursorPosition--;
                            RedrawLine(line, cursorPosition, promptLength);
                        }
                        break;

                    case ConsoleKey.Delete:
                        if (cursorPosition < line.Length)
                        {
                            line = line.Remove(cursorPosition, 1);
                            RedrawLine(line, cursorPosition, promptLength);
                        }
                        break;

                    case ConsoleKey.LeftArrow:
                        if (cursorPosition > 0)
                        {
                            cursorPosition--;
                            Console.SetCursorPosition(promptLength + cursorPosition, Console.CursorTop);
                        }
                        break;

                    case ConsoleKey.RightArrow:
                        if (cursorPosition < line.Length)
                        {
                            cursorPosition++;
                            Console.SetCursorPosition(promptLength + cursorPosition, Console.CursorTop);
                        }
                        break;

                    case ConsoleKey.Home:
                        cursorPosition = 0;
                        Console.SetCursorPosition(promptLength, Console.CursorTop);
                        break;

                    case ConsoleKey.End:
                        cursorPosition = line.Length;
                        Console.SetCursorPosition(promptLength + cursorPosition, Console.CursorTop);
                        break;

                    case ConsoleKey.UpArrow:
                        var prevCommand = GetPreviousCommand();
                        if (prevCommand != null)
                        {
                            line = prevCommand;
                            cursorPosition = line.Length;
                            RedrawLine(line, cursorPosition, promptLength);
                        }
                        break;

                    case ConsoleKey.DownArrow:
                        var nextCommand = GetNextCommand();
                        if (nextCommand != null)
                        {
                            line = nextCommand;
                            cursorPosition = line.Length;
                            RedrawLine(line, cursorPosition, promptLength);
                        }
                        else
                        {
                            line = "";
                            cursorPosition = 0;
                            RedrawLine(line, cursorPosition, promptLength);
                        }
                        break;

                    case ConsoleKey.Tab:
                        if (keyInfo.Modifiers != ConsoleModifiers.Shift)
                        {
                            var completion = GetCompletion(line, cursorPosition);
                            if (completion != null)
                            {
                                line = completion;
                                cursorPosition = line.Length;
                                RedrawLine(line, cursorPosition, promptLength);
                            }
                        }
                        break;

                    case ConsoleKey.Escape:
                        // Clear the line
                        line = "";
                        cursorPosition = 0;
                        RedrawLine(line, cursorPosition, promptLength);
                        break;

                    default:
                        if (!char.IsControl(keyInfo.KeyChar))
                        {
                            line = line.Insert(cursorPosition, keyInfo.KeyChar.ToString());
                            cursorPosition++;
                            RedrawLine(line, cursorPosition, promptLength);
                        }
                        break;
                }
            }
        }

        private void RedrawLine(string line, int cursorPosition, int promptLength)
        {
            // Clear the current line from cursor position
            Console.SetCursorPosition(promptLength, Console.CursorTop);
            Console.Write(new string(' ', Math.Max(0, Console.WindowWidth - promptLength - 1)));
            Console.SetCursorPosition(promptLength, Console.CursorTop);

            // Apply syntax highlighting for commands
            if (line.StartsWith('/'))
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write(line);
                Console.ResetColor();
            }
            else
            {
                Console.Write(line);
            }

            // Position cursor correctly
            Console.SetCursorPosition(promptLength + cursorPosition, Console.CursorTop);
        }

        private string GetModeHeader()
        {
            return _currentMode switch
            {
                Mode.Default => $"[{GetModeColor()}]Default Mode - General AI Assistant[/]",
                Mode.Planning => $"[{GetModeColor()}]Planning Mode - Strategic Analysis[/]",
                Mode.Orchestrator => $"[{GetModeColor()}]Orchestrator Mode - Complex Task Management[/]",
                _ => $"[{GetModeColor()}]Unknown Mode[/]"
            };
        }

        private string GetModeColor()
        {
            return _currentMode switch
            {
                Mode.Default => "cyan",
                Mode.Planning => "yellow",
                Mode.Orchestrator => "magenta",
                _ => "white"
            };
        }

        private void AddToHistory(string command)
        {
            if (_commandHistory.Count == 0 || _commandHistory[^1] != command)
            {
                _commandHistory.Add(command);
                if (_commandHistory.Count > 1000)
                {
                    _commandHistory.RemoveAt(0);
                }
            }
        }

        private string? GetPreviousCommand()
        {
            if (_commandHistory.Count == 0) return null;

            if (_historyIndex == -1)
            {
                _historyIndex = _commandHistory.Count - 1;
            }
            else if (_historyIndex > 0)
            {
                _historyIndex--;
            }

            return _historyIndex >= 0 ? _commandHistory[_historyIndex] : null;
        }

        private string? GetNextCommand()
        {
            if (_commandHistory.Count == 0 || _historyIndex == -1) return null;

            _historyIndex++;

            if (_historyIndex >= _commandHistory.Count)
            {
                _historyIndex = -1;
                return null;
            }

            return _commandHistory[_historyIndex];
        }

        private string? GetCompletion(string line, int cursorPosition)
        {
            if (_commandRegistry == null || !line.StartsWith('/')) return null;

            var commandPart = line[1..]; // Remove the '/' prefix
            var spaceIndex = commandPart.IndexOf(' ');

            // Only complete if cursor is in the command name part (before first space)
            // The cursor position is relative to the entire line, so we need to adjust for the '/'
            var adjustedCursorPosition = cursorPosition - 1; // Subtract 1 for the '/' prefix
            if (spaceIndex > 0 && adjustedCursorPosition > spaceIndex) return null;

            var commandPrefix = spaceIndex > 0 ? commandPart[..spaceIndex] : commandPart;

            var availableCommands = _commandRegistry.GetAllCommandNames()
                .Where(name => name.StartsWith(commandPrefix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(name => name)
                .ToList();

            if (availableCommands.Count == 1)
            {
                // Single match - complete it
                var completion = availableCommands[0];
                var remainder = spaceIndex > 0 ? commandPart[spaceIndex..] : "";
                return "/" + completion + remainder;
            }
            else if (availableCommands.Count > 1)
            {
                // Multiple matches - show them and try to complete common prefix
                ShowCompletionOptions(availableCommands, commandPrefix);

                var commonPrefix = FindCommonPrefix(availableCommands);
                if (commonPrefix.Length > commandPrefix.Length)
                {
                    var remainder = spaceIndex > 0 ? commandPart[spaceIndex..] : "";
                    return "/" + commonPrefix + remainder;
                }
            }

            return null;
        }

        private void ShowCompletionOptions(List<string> options, string prefix)
        {
            Console.WriteLine();

            // Create a nice completion panel
            var panel = new Panel(BuildCompletionTable(options, prefix))
            {
                Header = new PanelHeader("[dim]💡 Available Commands[/]"),
                Border = BoxBorder.Rounded,
                BorderStyle = Style.Parse("dim"),
                Padding = new Padding(1, 0, 1, 0)
            };

            AnsiConsole.Write(panel);
        }

        private Table BuildCompletionTable(List<string> options, string prefix)
        {
            var table = new Table();
            table.Border = TableBorder.None;
            table.ShowHeaders = false;

            var columns = Math.Min(3, options.Count);
            for (int i = 0; i < columns; i++)
            {
                table.AddColumn("");
            }

            for (int i = 0; i < options.Count; i += columns)
            {
                var row = new string[columns];
                for (int j = 0; j < columns && i + j < options.Count; j++)
                {
                    var option = options[i + j];
                    var highlighted = $"[green bold]{prefix}[/][white]{option[prefix.Length..]}[/]";
                    row[j] = highlighted;
                }
                table.AddRow(row);
            }

            return table;
        }

        private string FindCommonPrefix(List<string> strings)
        {
            if (strings.Count == 0) return "";
            if (strings.Count == 1) return strings[0];

            var commonPrefix = strings[0];
            for (int i = 1; i < strings.Count; i++)
            {
                commonPrefix = GetCommonPrefix(commonPrefix, strings[i]);
                if (commonPrefix.Length == 0) break;
            }

            return commonPrefix;
        }

        private string GetCommonPrefix(string str1, string str2)
        {
            var minLength = Math.Min(str1.Length, str2.Length);
            var commonLength = 0;

            for (int i = 0; i < minLength; i++)
            {
                if (char.ToLowerInvariant(str1[i]) == char.ToLowerInvariant(str2[i]))
                {
                    commonLength++;
                }
                else
                {
                    break;
                }
            }

            return str1[..commonLength];
        }
    }
}