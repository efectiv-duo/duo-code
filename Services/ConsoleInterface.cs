using System;
using System.Threading;
using System.Threading.Tasks;
using duo_code.Models;

namespace duo_code.Services
{
    public class ConsoleInterface
    {
        private const ConsoleColor UserColor = ConsoleColor.White;
        private const ConsoleColor AssistantColor = ConsoleColor.Yellow;
        private const ConsoleColor ToolColor = ConsoleColor.Cyan;
        private const ConsoleColor ErrorColor = ConsoleColor.Red;
        private const ConsoleColor InfoColor = ConsoleColor.Gray;

        public void ShowWelcomeMessage()
        {
            Console.ForegroundColor = InfoColor;
            Console.WriteLine("Welcome to Duo-Code AI Assistant!");
            Console.WriteLine("Type '/help' for available commands or '/exit' to quit.");
            
            var logger = new ConversationLogger();
            Console.WriteLine($"Conversation logs saved to: {logger.GetLogsDirectory()}");
            
            Console.WriteLine();
            Console.ResetColor();
        }

        public async Task<(string input, Mode? modeSwitch)> GetUserInputAsync(Mode currentMode)
        {
            Console.ForegroundColor = UserColor;
            Console.Write($"[{currentMode.ToDisplayString()}]> ");
            
            var input = await Task.Run(() =>
            {
                var line = "";
                ConsoleKeyInfo keyInfo;
                
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
            
            Console.ResetColor();
            
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
            Console.ForegroundColor = AssistantColor;
            Console.WriteLine(response);
            Console.ResetColor();
        }

        public void ShowToolExecution(string toolName)
        {
            Console.ForegroundColor = ToolColor;
            Console.WriteLine($"\n[Executing {toolName}...]");
            Console.ResetColor();
        }

        public void ShowToolResult(string result)
        {
            Console.ForegroundColor = ToolColor;
            Console.WriteLine(result);
            Console.ResetColor();
        }

        public void ShowError(string error)
        {
            Console.ForegroundColor = ErrorColor;
            Console.WriteLine(error);
            Console.ResetColor();
        }

        public void ShowInfo(string info)
        {
            Console.ForegroundColor = InfoColor;
            Console.WriteLine(info);
            Console.ResetColor();
        }

        public void ShowThinking()
        {
            Console.ForegroundColor = InfoColor;
            Console.Write("\rThinking...");
            Console.ResetColor();
        }

        public void ClearThinking()
        {
            Console.Write("\r            \r");
        }

        public void SetupCancellation(CancellationTokenSource cts)
        {
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };
        }

        public bool WaitForContinueOrCancel()
        {
            Console.ForegroundColor = InfoColor;
            Console.Write("\nPress [Space] to continue or [Esc] to cancel...");
            Console.ResetColor();
            
            while (true)
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Spacebar)
                {
                    Console.WriteLine(" [Continue]");
                    return true;
                }
                else if (key.Key == ConsoleKey.Escape)
                {
                    Console.WriteLine(" [Cancelled]");
                    return false;
                }
                // Ignore other keys and continue waiting
            }
        }
    }
}