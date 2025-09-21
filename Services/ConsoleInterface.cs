using System;
using System.Threading;
using System.Threading.Tasks;
using duo_code.Commands.Core;
using Spectre.Console;

namespace duo_code.Services
{
    public class ConsoleInterface
    {
        private static readonly string[] ThinkingFrames = { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
        private CancellationTokenSource? _thinkingCancellation;
        private CommandRegistry? _commandRegistry;

        public ConsoleInterface(CommandRegistry commandRegistry) 
        {
            _commandRegistry = commandRegistry;
        }

        public void ShowWelcomeMessage()
        {
            WriteLine();

            AnsiConsole.Write(
                new FigletText("duocode")
                    .Centered()
                    .Color(Color.Green));

            WriteLine();

            var logger = new ConversationLogger();
            AnsiConsole.MarkupLine("[dim]Type [bold]/help[/] for commands. [bold]/exit[/] to quit.[/]");
            WriteInfo($"Conversation logs saved to: {logger.GetLogsDirectory()}");
            WriteInfo($"Current working directory: {Directory.GetCurrentDirectory()}");
            AnsiConsole.WriteLine();
        }

        private readonly List<string> _commandHistory = new();
        private int _historyIndex = -1;
        
        public void SetCommandRegistry(CommandRegistry commandRegistry)
        {
            _commandRegistry = commandRegistry;
        }
        
        public async Task<string> GetUserInputAsync()
        {
            // Simple console read implementation
            ShowPrompt();
            var input = await Task.Run(() => Console.ReadLine() ?? string.Empty);

            // Add to history if not empty
            if (!string.IsNullOrWhiteSpace(input))
            {
                AddToHistory(input);
            }

            return input;
        }
        
        private string GetUserInputWithCustomHandling()
        {
            var customInput = new CustomTextInput(_commandRegistry, _commandHistory);
            var result = customInput.ReadInput();

            // Save command history
            if (!string.IsNullOrWhiteSpace(result))
            {
                SaveCommandHistory();
            }

            return result;
        }
        
        private void RedrawLine(string line, int cursorPosition, int promptLength)
        {
            // Clear the current line
            Console.SetCursorPosition(promptLength, Console.CursorTop);
            Console.Write(new string(' ', Console.WindowWidth - promptLength - 1));
            Console.SetCursorPosition(promptLength, Console.CursorTop);
            
            // Write the new line
            Console.Write(line);
            
            // Position cursor correctly
            Console.SetCursorPosition(promptLength + cursorPosition, Console.CursorTop);
        }
        
        private void AddToHistory(string command)
        {
            // Don't add duplicate consecutive commands
            if (_commandHistory.Count == 0 || _commandHistory[^1] != command)
            {
                _commandHistory.Add(command);
                
                // Keep history size manageable
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
        
        private void LoadCommandHistory()
        {
            try
            {
                var historyPath = GetHistoryFilePath();
                if (File.Exists(historyPath))
                {
                    var lines = File.ReadAllLines(historyPath);
                    _commandHistory.Clear();
                    _commandHistory.AddRange(lines.Where(l => !string.IsNullOrWhiteSpace(l)));
                }
            }
            catch
            {
                // Ignore errors loading history
            }
        }
        
        private void SaveCommandHistory()
        {
            try
            {
                var historyPath = GetHistoryFilePath();
                var directory = Path.GetDirectoryName(historyPath);
                if (directory != null && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                // Save last 500 commands
                var commandsToSave = _commandHistory.TakeLast(500);
                File.WriteAllLines(historyPath, commandsToSave);
            }
            catch
            {
                // Ignore errors saving history
            }
        }
        
        private string GetHistoryFilePath()
        {
            var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(homeDirectory, ".duo-code-history");
        }
        
        private string? GetCompletion(string line, int cursorPosition)
        {
            if (_commandRegistry == null || !line.StartsWith('/')) return null;
            
            // Only complete commands at the beginning
            var commandPart = line[1..]; // Remove the '/' prefix
            var spaceIndex = commandPart.IndexOf(' ');
            
            // Only complete if cursor is in the command name part (before first space)
            if (spaceIndex > 0 && cursorPosition > spaceIndex + 1) return null;
            
            var commandPrefix = spaceIndex > 0 ? commandPart[..spaceIndex] : commandPart;
            
            var availableCommands = _commandRegistry.GetCommandList()
                .Select(cmd => cmd.name)
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
                // Multiple matches - show them
                ShowCompletionOptions(availableCommands, commandPrefix);
                
                // Find common prefix
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
            
            var table = new Table();
            table.Border = TableBorder.None;
            table.ShowHeaders = false;
            
            // Show completions in columns
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
                    // Highlight the matching prefix
                    var highlighted = $"[green]{prefix}[/]{option[prefix.Length..]}";
                    row[j] = highlighted;
                }
                table.AddRow(row);
            }
            
            AnsiConsole.Write(table);
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
        
        

        
        private void ShowPrompt()
        {
            AnsiConsole.Markup("[magenta]>[/] ");
        }

        public void ShowError(string error)
        {
            var panel = new Panel(new Text(error))
            {
                Header = new PanelHeader("[red]Error[/]"),
                Border = BoxBorder.Heavy,
                BorderStyle = Style.Parse("red")
            };
            
            AnsiConsole.Write(panel);
        }

        public void ShowInfo(string info)
        {
            AnsiConsole.WriteLine(info, Style.Parse("dim"));
        }
        

        public void ShowThinking()
        {
            _thinkingCancellation = new CancellationTokenSource();
            
            Task.Run(async () =>
            {
                var frameIndex = 0;
                while (!_thinkingCancellation.Token.IsCancellationRequested)
                {
                    Console.Write($"\r{ThinkingFrames[frameIndex]} Thinking...");
                    frameIndex = (frameIndex + 1) % ThinkingFrames.Length;
                    
                    try
                    {
                        await Task.Delay(100, _thinkingCancellation.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }, _thinkingCancellation.Token);
        }

        public void ClearThinking()
        {
            try
            {
                _thinkingCancellation?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed, ignore
            }
            Console.Write("\r                    \r");
        }

        public void SetupCancellation(CancellationTokenSource cts)
        {
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                
                try
                {
                    if (!cts.IsCancellationRequested)
                    {
                        cts.Cancel();
                    }
                }
                catch (ObjectDisposedException)
                {
                    // CTS already disposed, ignore
                }
                
                // Show graceful exit message
                Console.WriteLine();
                var panel = new Panel(new Text("Operation cancelled by user."))
                {
                    Header = new PanelHeader("[yellow]Cancelled[/]"),
                    Border = BoxBorder.Rounded,
                    BorderStyle = Style.Parse("yellow")
                };
                AnsiConsole.Write(panel);
            };
        }

        public bool WaitForContinueOrCancel()
        {
            WriteInfo("Press [Space] to continue or [Esc] to cancel...");

            var result = false;
            while (true)
            {
                var key = Console.ReadKey(true);

                if (key.Key == ConsoleKey.Spacebar)
                {
                    result = true;
                    break;
                }
                else if (key.Key == ConsoleKey.Escape)
                {
                    result = false;
                    break;
                }
            }

            Console.Write("\x1b[1A\x1b[2K");

            return result;
        }

        public void ShowProgress(string description, Action action)
        {
            AnsiConsole.Status()
                .Start(description, ctx =>
                {
                    action();
                });
        }

        public async Task ShowProgressAsync(string description, Func<Task> asyncAction)
        {
            await AnsiConsole.Status()
                .StartAsync(description, async ctx =>
                {
                    await asyncAction();
                });
        }

        private static string EscapeMarkup(string text)
        {
            return text.Replace("[", "[[").Replace("]", "]]");
        }
    }
}