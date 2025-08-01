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
        private readonly AgentMode _currentMode;
        private readonly FileSearchService _fileSearchService;
        private int _historyIndex = -1;
        private List<FileSearchResult> _fileSuggestions = new();
        private int _selectedSuggestionIndex = 0;
        private bool _showingSuggestions = false;
        private string _currentFileSearchTerm = "";
        
        // Terminal state management
        private int _inputLineTop = -1;
        private int _suggestionStartLine = -1;
        private int _lastSuggestionCount = 0;
        private string _lastRenderedLine = "";
        
        // Page-through suggestions state
        private const int SuggestionsPerPage = 10;
        private List<FileSearchResult> _allMatchingSuggestions = new();
        private List<FileSearchResult> _currentPageSuggestions = new();
        private int _currentPageStartIndex = 0;
        private int _totalSuggestionCount = 0;
        private bool _hasMorePages = false;
        private bool _hasPreviousPages = false;
        private string _lastSearchTermForPaging = "";
        
        // Viewport adjustment state
        private int _originalInputLineTop = -1;
        private bool _inputRepositioned = false;
        private const int RequiredSuggestionLines = SuggestionsPerPage;

        public CustomTextInput(CommandRegistry? commandRegistry, List<string> commandHistory, AgentMode currentMode)
        {
            _commandRegistry = commandRegistry;
            _commandHistory = commandHistory;
            _currentMode = currentMode;
            _fileSearchService = new FileSearchService();
        }

        public (string input, AgentMode? modeSwitch) ReadInput()
        {
            // Create a styled input box
            var panel = new Spectre.Console.Panel("")
            {
                Header = new PanelHeader(GetModeHeader()),
                Border = BoxBorder.Rounded,
                BorderStyle = Style.Parse(GetModeColor()),
                Padding = new Spectre.Console.Padding(1, 0, 1, 0)
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
                AgentMode.Default => "🤖",
                AgentMode.Planning => "📋",
                AgentMode.Orchestrator => "🎯",
                _ => "❓"
            };
            
            AnsiConsole.Write(new Rule($"[bold {GetModeColor()}]{modeEmoji} {_currentMode.ToDisplayString()} AgentMode[/]")
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
            
            // Initialize input line position
            _inputLineTop = Console.CursorTop;
            _originalInputLineTop = _inputLineTop;
            _lastRenderedLine = "";

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
                        if (_showingSuggestions && _fileSuggestions.Count > 0)
                        {
                            // Auto-complete with selected file and continue editing
                            var selectedFile = _fileSuggestions[_selectedSuggestionIndex];
                            var completed = ApplyFileCompletion(line, cursorPosition, selectedFile.RelativePath);
                            if (completed.HasValue)
                            {
                                line = completed.Value.newLine;
                                cursorPosition = completed.Value.newCursorPosition;
                                ClearFileSuggestions();
                                RedrawLine(line, cursorPosition, promptLength);
                                continue; // Don't submit, let user continue editing
                            }
                        }
                        
                        // Clear suggestions and move to next line for output
                        ClearFileSuggestions();
                        Console.SetCursorPosition(0, _inputLineTop + 1);
                        
                        // Reset input line tracking for next input
                        _inputLineTop = -1;
                        _originalInputLineTop = -1;
                        _inputRepositioned = false;
                        _lastRenderedLine = "";
                        
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
                            
                            UpdateFileSuggestions(line, cursorPosition);
                            RedrawLine(line, cursorPosition, promptLength);
                        }
                        break;

                    case ConsoleKey.Delete:
                        if (cursorPosition < line.Length)
                        {
                            line = line.Remove(cursorPosition, 1);
                            
                            UpdateFileSuggestions(line, cursorPosition);
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
                        if (_showingSuggestions && _fileSuggestions.Count > 0)
                        {
                            var oldIndex = _selectedSuggestionIndex;
                            
                            // Check if we're at the top of current page and need to go to previous page
                            if (_selectedSuggestionIndex == 0 && _hasPreviousPages)
                            {
                                LoadPreviousPage();
                                _selectedSuggestionIndex = _fileSuggestions.Count - 1; // Highlight last item of previous page
                                oldIndex = -1; // Force full redraw since we changed pages
                            }
                            else if (_selectedSuggestionIndex > 0)
                            {
                                _selectedSuggestionIndex--;
                            }
                            
                            if (oldIndex == -1)
                            {
                                ShowFileSuggestions(); // Full redraw for page change
                            }
                            else
                            {
                                UpdateSuggestionHighlight(oldIndex, _selectedSuggestionIndex);
                            }
                        }
                        else
                        {
                            var prevCommand = GetPreviousCommand();
                            if (prevCommand != null)
                            {
                                line = prevCommand;
                                cursorPosition = line.Length;
                                RedrawLine(line, cursorPosition, promptLength);
                            }
                        }
                        break;

                    case ConsoleKey.DownArrow:
                        if (_showingSuggestions && _fileSuggestions.Count > 0)
                        {
                            var oldIndex = _selectedSuggestionIndex;
                            
                            // Check if we're at the bottom of current page and need to go to next page
                            if (_selectedSuggestionIndex == _fileSuggestions.Count - 1 && _hasMorePages)
                            {
                                LoadNextPage();
                                _selectedSuggestionIndex = 0; // Highlight first item of new page
                                oldIndex = -1; // Force full redraw since we changed pages
                            }
                            else if (_selectedSuggestionIndex < _fileSuggestions.Count - 1)
                            {
                                _selectedSuggestionIndex++;
                            }
                            
                            if (oldIndex == -1)
                            {
                                ShowFileSuggestions(); // Full redraw for page change
                            }
                            else
                            {
                                UpdateSuggestionHighlight(oldIndex, _selectedSuggestionIndex);
                            }
                        }
                        else
                        {
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
                        }
                        break;

                    case ConsoleKey.Tab:
                        if (keyInfo.Modifiers != ConsoleModifiers.Shift)
                        {
                            if (_showingSuggestions && _fileSuggestions.Count > 0)
                            {
                                // Auto-complete with selected file
                                var selectedFile = _fileSuggestions[_selectedSuggestionIndex];
                                var completed = ApplyFileCompletion(line, cursorPosition, selectedFile.RelativePath);
                                if (completed.HasValue)
                                {
                                    line = completed.Value.newLine;
                                    cursorPosition = completed.Value.newCursorPosition;
                                    ClearFileSuggestions();
                                    RedrawLine(line, cursorPosition, promptLength);
                                }
                            }
                            else
                            {
                                // Regular command completion
                                var completion = GetCompletion(line, cursorPosition);
                                if (completion != null)
                                {
                                    line = completion;
                                    cursorPosition = line.Length;
                                    RedrawLine(line, cursorPosition, promptLength);
                                }
                            }
                        }
                        break;

                    case ConsoleKey.Escape:
                        if (_showingSuggestions)
                        {
                            ClearFileSuggestions();
                        }
                        else
                        {
                            // Clear the line
                            line = "";
                            cursorPosition = 0;
                            RedrawLine(line, cursorPosition, promptLength);
                        }
                        break;

                    default:
                        if (!char.IsControl(keyInfo.KeyChar))
                        {
                            line = line.Insert(cursorPosition, keyInfo.KeyChar.ToString());
                            cursorPosition++;
                            
                            UpdateFileSuggestions(line, cursorPosition);
                            RedrawLine(line, cursorPosition, promptLength);
                        }
                        break;
                }
            }
        }

        private void RedrawLine(string line, int cursorPosition, int promptLength)
        {
            // Store the input line position if not set
            if (_inputLineTop == -1)
            {
                _inputLineTop = Console.CursorTop;
            }
            
            // Always go to the fixed input line position
            Console.SetCursorPosition(promptLength, _inputLineTop);
            
            // Clear the entire line if content changed
            if (_lastRenderedLine != line)
            {
                Console.Write(new string(' ', Math.Max(0, Console.WindowWidth - promptLength - 1)));
                Console.SetCursorPosition(promptLength, _inputLineTop);
                
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
                
                _lastRenderedLine = line;
            }

            // Position cursor correctly
            Console.SetCursorPosition(promptLength + cursorPosition, _inputLineTop);
        }

        private string GetModeHeader()
        {
            return _currentMode switch
            {
                AgentMode.Default => $"[{GetModeColor()}]Default AgentMode - General AI Assistant[/]",
                AgentMode.Planning => $"[{GetModeColor()}]Planning AgentMode - Strategic Analysis[/]",
                AgentMode.Orchestrator => $"[{GetModeColor()}]Orchestrator AgentMode - Complex Task Management[/]",
                _ => $"[{GetModeColor()}]Unknown AgentMode[/]"
            };
        }

        private string GetModeColor()
        {
            return _currentMode switch
            {
                AgentMode.Default => "cyan",
                AgentMode.Planning => "yellow",
                AgentMode.Orchestrator => "magenta",
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
            var panel = new Spectre.Console.Panel(BuildCompletionTable(options, prefix))
            {
                Header = new PanelHeader("[dim]💡 Available Commands[/]"),
                Border = BoxBorder.Rounded,
                BorderStyle = Style.Parse("dim"),
                Padding = new Spectre.Console.Padding(1, 0, 1, 0)
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

        private void UpdateFileSuggestions(string line, int cursorPosition)
        {
            var (atIndex, searchTerm) = FindFileReferenceContext(line, cursorPosition);
            
            if (atIndex == -1 || searchTerm.Length < 1)
            {
                ClearFileSuggestions();
                return;
            }
            
            // Don't search for very short terms unless they're meaningful
            if (searchTerm.Length < 1 || (searchTerm.Length == 1 && !char.IsUpper(searchTerm[0])))
            {
                ClearFileSuggestions(); 
                return;
            }
            
            // Only update if search term actually changed to avoid unnecessary refreshes
            if (_currentFileSearchTerm == searchTerm && _showingSuggestions)
            {
                return;
            }
            
            // Update search term and get all matching results
            _currentFileSearchTerm = searchTerm;
            _lastSearchTermForPaging = searchTerm;
            
            var allResults = _fileSearchService.SearchFiles(searchTerm, Directory.GetCurrentDirectory(), int.MaxValue);
            
            if (allResults.Count > 0)
            {
                // Ensure we have adequate viewport space
                EnsureViewportSpace();
                
                // Clear previous suggestions first to prevent accumulation
                if (_showingSuggestions)
                {
                    ClearSuggestionArea();
                }
                
                _allMatchingSuggestions = allResults;
                _totalSuggestionCount = allResults.Count;
                _currentPageStartIndex = 0;
                
                LoadCurrentPage();
                
                _selectedSuggestionIndex = 0;
                _showingSuggestions = true;
                ShowFileSuggestions();
            }
            else
            {
                ClearFileSuggestions();
            }
        }
        
        private void ShowFileSuggestions()
        {
            if (!_showingSuggestions || _fileSuggestions.Count == 0) return;
            
            // Clear any previous suggestions first
            ClearSuggestionArea();
            
            // Set suggestion start line (right below input line)
            _suggestionStartLine = _inputLineTop + 1;
            
            // Render each suggestion
            for (int i = 0; i < _fileSuggestions.Count; i++)
            {
                if (_suggestionStartLine + i >= Console.BufferHeight) break;
                
                RenderSuggestionLine(i, i == _selectedSuggestionIndex);
            }
            
            _lastSuggestionCount = _fileSuggestions.Count;
            
            // Return cursor to input line
            Console.SetCursorPosition(Console.CursorLeft, _inputLineTop);
        }
        
        private void RenderSuggestionLine(int index, bool isSelected)
        {
            if (index >= _fileSuggestions.Count) return;
            
            Console.SetCursorPosition(0, _suggestionStartLine + index);
            
            var suggestion = _fileSuggestions[index];
            
            if (isSelected)
            {
                Console.BackgroundColor = ConsoleColor.DarkBlue;
                Console.ForegroundColor = ConsoleColor.White;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
            }
            
            var directory = Path.GetDirectoryName(suggestion.RelativePath);
            var displayPath = string.IsNullOrEmpty(directory) ? suggestion.FileName : 
                $"{suggestion.FileName} ({directory})";
            
            // Write suggestion and clear rest of line
            Console.Write($"  {displayPath}");
            var remainingWidth = Console.WindowWidth - Console.CursorLeft;
            if (remainingWidth > 0)
            {
                Console.Write(new string(' ', remainingWidth));
            }
            
            Console.ResetColor();
        }
        
        private void UpdateSuggestionHighlight(int oldIndex, int newIndex)
        {
            if (!_showingSuggestions || _fileSuggestions.Count == 0) return;
            
            var currentCursorLeft = Console.CursorLeft;
            
            // Un-highlight the old selection
            if (oldIndex >= 0 && oldIndex < _fileSuggestions.Count)
            {
                RenderSuggestionLine(oldIndex, false);
            }
            
            // Highlight the new selection
            if (newIndex >= 0 && newIndex < _fileSuggestions.Count)
            {
                RenderSuggestionLine(newIndex, true);
            }
            
            // Return cursor to input line
            Console.SetCursorPosition(currentCursorLeft, _inputLineTop);
        }
        
        private void ClearFileSuggestions()
        {
            if (!_showingSuggestions && _lastSuggestionCount == 0) return;
            
            var currentLeft = Console.CursorLeft;
            
            ClearSuggestionArea();
            
            // Restore original input position if it was repositioned
            RestoreOriginalInputPosition();
            
            // Reset state
            _showingSuggestions = false;
            _fileSuggestions.Clear();
            _currentPageSuggestions.Clear();
            _allMatchingSuggestions.Clear();
            _selectedSuggestionIndex = 0;
            _currentFileSearchTerm = "";
            _lastSearchTermForPaging = "";
            _lastSuggestionCount = 0;
            _currentPageStartIndex = 0;
            _totalSuggestionCount = 0;
            _hasMorePages = false;
            _hasPreviousPages = false;
            
            // Return cursor to input line
            Console.SetCursorPosition(currentLeft, _inputLineTop);
        }
        
        private void ClearSuggestionArea()
        {
            // Only clear if we have suggestions to clear
            if (_lastSuggestionCount == 0 && _suggestionStartLine == -1) return;
            
            var linesToClear = Math.Max(RequiredSuggestionLines, _lastSuggestionCount);
            var startLine = _suggestionStartLine != -1 ? _suggestionStartLine : _inputLineTop + 1;
            
            for (int i = 0; i < linesToClear; i++)
            {
                var lineToCheck = startLine + i;
                if (lineToCheck < Console.BufferHeight)
                {
                    Console.SetCursorPosition(0, lineToCheck);
                    Console.Write(new string(' ', Console.WindowWidth));
                }
            }
            
            _suggestionStartLine = -1;
        }
        
        private (string newLine, int newCursorPosition)? ApplyFileCompletion(string line, int cursorPosition, string filePath)
        {
            var (atIndex, searchTerm) = FindFileReferenceContext(line, cursorPosition);
            if (atIndex == -1) return null;
            
            // Replace the search term with the complete file path
            var beforeAt = line.Substring(0, atIndex + 1); // Include the @
            var afterSearchTerm = line.Substring(cursorPosition);
            
            var newLine = beforeAt + filePath + afterSearchTerm;
            var newCursorPosition = beforeAt.Length + filePath.Length;
            
            return (newLine, newCursorPosition);
        }
        
        private (int atIndex, string searchTerm) FindFileReferenceContext(string line, int cursorPosition)
        {
            // Find the last @ symbol before the cursor
            var atIndex = -1;
            for (int i = cursorPosition - 1; i >= 0; i--)
            {
                if (line[i] == '@')
                {
                    atIndex = i;
                    break;
                }
                if (char.IsWhiteSpace(line[i]))
                {
                    // Found whitespace before @, stop searching
                    break;
                }
            }
            
            if (atIndex == -1) return (-1, string.Empty);
            
            var searchTerm = line.Substring(atIndex + 1, cursorPosition - atIndex - 1);
            return (atIndex, searchTerm);
        }
        
        private void LoadCurrentPage()
        {
            var pageSize = Math.Min(SuggestionsPerPage, _allMatchingSuggestions.Count - _currentPageStartIndex);
            _currentPageSuggestions = _allMatchingSuggestions
                .Skip(_currentPageStartIndex)
                .Take(pageSize)
                .ToList();
            
            _fileSuggestions = _currentPageSuggestions;
            
            // Update pagination flags
            _hasPreviousPages = _currentPageStartIndex > 0;
            _hasMorePages = _currentPageStartIndex + pageSize < _allMatchingSuggestions.Count;
        }
        
        private void LoadNextPage()
        {
            if (!_hasMorePages) return;
            
            _currentPageStartIndex += SuggestionsPerPage;
            LoadCurrentPage();
        }
        
        private void LoadPreviousPage()
        {
            if (!_hasPreviousPages) return;
            
            _currentPageStartIndex = Math.Max(0, _currentPageStartIndex - SuggestionsPerPage);
            LoadCurrentPage();
        }
        
        private void EnsureViewportSpace()
        {
            var availableLines = Console.BufferHeight - _inputLineTop - 1;
            var requiredLines = RequiredSuggestionLines;
            
            if (availableLines < requiredLines)
            {
                var linesToMove = requiredLines - availableLines;
                var newInputTop = Math.Max(0, _inputLineTop - linesToMove);
                
                if (newInputTop != _inputLineTop)
                {
                    // Clear current input line
                    Console.SetCursorPosition(0, _inputLineTop);
                    Console.Write(new string(' ', Console.WindowWidth));
                    
                    // Move input to new position
                    _inputLineTop = newInputTop;
                    _inputRepositioned = true;
                    
                    // Redraw input at new position
                    Console.SetCursorPosition(0, _inputLineTop);
                    var promptText = $"[{GetModeColor()}]> [/]";
                    AnsiConsole.Markup(promptText);
                    
                    if (!string.IsNullOrEmpty(_lastRenderedLine))
                    {
                        if (_lastRenderedLine.StartsWith('/'))
                        {
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.Write(_lastRenderedLine);
                            Console.ResetColor();
                        }
                        else
                        {
                            Console.Write(_lastRenderedLine);
                        }
                    }
                }
            }
        }
        
        private void RestoreOriginalInputPosition()
        {
            if (!_inputRepositioned) return;
            
            // Clear current input and suggestion area
            for (int i = _inputLineTop; i <= _inputLineTop + RequiredSuggestionLines; i++)
            {
                if (i < Console.BufferHeight)
                {
                    Console.SetCursorPosition(0, i);
                    Console.Write(new string(' ', Console.WindowWidth));
                }
            }
            
            // Restore to original position
            _inputLineTop = _originalInputLineTop;
            _inputRepositioned = false;
            
            // Redraw input at original position
            Console.SetCursorPosition(0, _inputLineTop);
            var promptText = $"[{GetModeColor()}]> [/]";
            AnsiConsole.Markup(promptText);
            
            if (!string.IsNullOrEmpty(_lastRenderedLine))
            {
                if (_lastRenderedLine.StartsWith('/'))
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write(_lastRenderedLine);
                    Console.ResetColor();
                }
                else
                {
                    Console.Write(_lastRenderedLine);
                }
            }
        }
    }
}