using System;
using System.Threading;
using System.Threading.Tasks;
using duo_code.Models;
using Spectre.Console;

namespace duo_code.Services
{
    public class SpectreConsoleInterface
    {
        private static readonly string[] ThinkingFrames = { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
        private CancellationTokenSource? _thinkingCancellation;

        public void ShowWelcomeMessage()
        {
            var rule = new Rule("[bold blue]Duo-Code AI Assistant[/]")
            {
                Style = Style.Parse("blue")
            };
            AnsiConsole.Write(rule);
            
            var panel = new Panel(new Markup(
                "[dim]Type [bold]/help[/] for available commands or [bold]/exit[/] to quit.[/]\n" +
                "[dim]Use [bold cyan]Shift+Tab[/] to switch between modes.[/]"))
            {
                Header = new PanelHeader("[green]Welcome[/]"),
                Border = BoxBorder.Rounded,
                BorderStyle = Style.Parse("green")
            };
            
            AnsiConsole.Write(panel);
            
            var logger = new ConversationLogger();
            Console.WriteLine($"Conversation logs saved to: {logger.GetLogsDirectory()}");
            AnsiConsole.WriteLine();
        }

        public async Task<(string input, Mode? modeSwitch)> GetUserInputAsync(Mode currentMode)
        {
            // Handle custom key input for mode switching
            var input = await Task.Run(() =>
            {
                var line = "";
                ConsoleKeyInfo keyInfo;
                
                Console.Write($"[{currentMode.ToDisplayString()}]> ");
                
                while (true)
                {
                    keyInfo = Console.ReadKey(true);
                    
                    // Check for Shift+Tab
                    if (keyInfo.Key == ConsoleKey.Tab && keyInfo.Modifiers == ConsoleModifiers.Shift)
                    {
                        return "SWITCH_MODE";
                    }
                    
                    // Handle backspace
                    if (keyInfo.Key == ConsoleKey.Backspace)
                    {
                        if (line.Length > 0)
                        {
                            line = line[..^1];
                            Console.Write("\b \b");
                        }
                        continue;
                    }
                    
                    // Handle enter
                    if (keyInfo.Key == ConsoleKey.Enter)
                    {
                        Console.WriteLine();
                        return line;
                    }
                    
                    // Handle regular characters
                    if (!char.IsControl(keyInfo.KeyChar))
                    {
                        line += keyInfo.KeyChar;
                        Console.Write(keyInfo.KeyChar);
                    }
                }
            });
            
            if (input == "SWITCH_MODE")
            {
                var nextMode = GetNextMode(currentMode);
                ShowInfo($"Switched to {nextMode.ToDisplayString()} mode");
                return ("", nextMode);
            }
            
            return (input ?? string.Empty, null);
        }

        private Mode GetNextMode(Mode currentMode)
        {
            return currentMode switch
            {
                Mode.Default => Mode.Planning,
                Mode.Planning => Mode.Orchestrator,
                Mode.Orchestrator => Mode.Default,
                _ => Mode.Default
            };
        }

        public void ShowAssistantResponse(string response)
        {
            var panel = new Panel(new Text(response))
            {
                Header = new PanelHeader("[yellow]Assistant[/]"),
                Border = BoxBorder.Rounded,
                BorderStyle = Style.Parse("yellow")
            };
            
            AnsiConsole.Write(panel);
        }

        public void ShowToolExecution(string toolName)
        {
            Console.WriteLine($"Executing {toolName}...");
        }

        public void ShowToolResult(string result)
        {
            var panel = new Panel(new Text(result))
            {
                Header = new PanelHeader("[cyan]Tool Result[/]"),
                Border = BoxBorder.Rounded,
                BorderStyle = Style.Parse("cyan")
            };
            
            AnsiConsole.Write(panel);
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
            Console.WriteLine("Press [Space] to continue or [Esc] to cancel...");
            
            while (true)
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Spacebar)
                {
                    Console.WriteLine("✓ Continue");
                    return true;
                }
                else if (key.Key == ConsoleKey.Escape)
                {
                    Console.WriteLine("✗ Cancelled");
                    return false;
                }
            }
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