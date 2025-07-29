using System;
using System.Threading.Tasks;
using duo_code.Services;
using duo_code.Services.Configuration;
using duo_code.Services.Interfaces;
using duo_code.Commands.Core;
using duo_code.Tools.Core;

namespace duo_code
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            // Setup global graceful exit handler
            var globalCts = new CancellationTokenSource();
            var ctrlCPressCount = 0;
            Console.CancelKeyPress += (sender, e) =>
            {
                ctrlCPressCount++;
                
                if (ctrlCPressCount == 1)
                {
                    e.Cancel = true;
                    try
                    {
                        if (!globalCts.Token.IsCancellationRequested)
                        {
                            globalCts.Cancel();
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        // Already disposed, ignore
                    }
                    Console.WriteLine("\nGracefully shutting down... Press Ctrl+C again to force exit.");
                }
                else
                {
                    Console.WriteLine("\nForce exit requested.");
                    Environment.Exit(1);
                }
            };

            try
            {
                // Check if running as subagent
                bool isSubagent = args.Contains("--subagent");
                string? subagentPrompt = null;
                
                if (isSubagent)
                {
                    // Find the prompt argument (everything after --subagent)
                    var subagentIndex = Array.IndexOf(args, "--subagent");
                    if (subagentIndex >= 0 && subagentIndex < args.Length - 1)
                    {
                        subagentPrompt = args[subagentIndex + 1];
                    }
                }
                
                // TODO: remove this before release
                // Directory.SetCurrentDirectory("C:\\Work\\Efectiv Duo\\projects\\duo-code");
                // Directory.SetCurrentDirectory(@"C:\Work\Efectiv Duo\clients\helpship\helpship.web");

                // Initialize configuration
                var config = new ConfigurationService();
                
                // Load saved model and provider settings
                var (savedProvider, savedModel) = ApiKeyManager.LoadCurrentSettings();
                duo_code.Services.ApiSettings.CurrentProvider = savedProvider;
                duo_code.Services.ApiSettings.CurrentModel = savedModel;
                
                // Show current settings
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Loaded settings: {savedProvider} - {savedModel}");
                Console.ResetColor();
                
                // Initialize API key
                var apiKey = await InitializeApiKey(args, config);
                if (string.IsNullOrEmpty(apiKey))
                {
                    Console.WriteLine("API key is required!");
                    return;
                }

                // Initialize services
                var commandRegistry = new CommandRegistry();
                var toolRegistry = new ToolRegistry();
                var responseProcessor = new StreamingResponseService();
                var consoleInterface = new SpectreConsoleInterface();
                var fileReferenceService = new FileReferenceService();
                var conversationState = new ConversationState
                {
                    CurrentModel = config.Settings.Models.DefaultModel
                };

                // Create agent service
                var agentService = new AgentService(
                    commandRegistry,
                    toolRegistry,
                    responseProcessor,
                    consoleInterface,
                    conversationState,
                    fileReferenceService
                );

                if (isSubagent && !string.IsNullOrWhiteSpace(subagentPrompt))
                {
                    // Subagent mode - process the given prompt and exit
                    await agentService.ProcessSubagentPromptAsync(subagentPrompt);
                }
                else
                {
                    // Normal interactive mode
                    await agentService.RunAsync(globalCts.Token);
                }
            }
            catch (OperationCanceledException) when (globalCts.Token.IsCancellationRequested)
            {
                Console.WriteLine("\nApplication shutdown completed.");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Fatal error: {ex.Message}");
                Console.ResetColor();
            }
            finally
            {
                globalCts?.Dispose();
            }
        }

        private static async Task<string> InitializeApiKey(string[] args, ConfigurationService config)
        {
            // Check command line args
            if (args.Length > 0 && args[0].StartsWith("--api-key="))
            {
                var apiKey = args[0].Substring("--api-key=".Length);
                ApiKeyManager.SaveApiKey(apiKey);
                return apiKey;
            }

            // Try to load from saved location
            var savedKey = ApiKeyManager.LoadApiKey();
            if (!string.IsNullOrWhiteSpace(savedKey))
                return savedKey;

            // Prompt user for key
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Please enter your Cerebras API key:");
            Console.ResetColor();
            
            var inputKey = await Task.Run(() => Console.ReadLine());
            if (!string.IsNullOrWhiteSpace(inputKey))
            {
                ApiKeyManager.SaveApiKey(inputKey);
                return inputKey;
            }

            return string.Empty;
        }
    }
}