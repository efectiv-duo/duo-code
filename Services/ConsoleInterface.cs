using System;
using System.Threading;
using System.Threading.Tasks;

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

        public async Task<string> GetUserInputAsync()
        {
            Console.ForegroundColor = UserColor;
            Console.Write("> ");
            var input = await Task.Run(() => Console.ReadLine());
            Console.ResetColor();
            return input ?? string.Empty;
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
    }
}