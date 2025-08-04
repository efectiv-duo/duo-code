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
            var globalCts = GracefulShutdownHandler.Setup();

            try
            {
                // Check if running as subagent
                var subagentPrompt = ArgumentParser.ParseSubagentPrompt(args);

                // Initialize services
                var commandRegistry = new CommandRegistry();
                var toolRegistry = new ToolRegistry();
                var responseProcessor = new StreamingResponseService();
                var consoleInterface = new ConsoleInterface(commandRegistry);
                var fileReferenceService = new FileReferenceService();

                // Load saved model and provider settings
                var apiSettings = ApiKeyManager.LoadCurrentSettings();
                // Load them into the state
                CurrentState.UpdateModelAndProvider(apiSettings.Provider, apiSettings.Model);
                
                // Create agent service
                var agentService = new AgentService(
                    commandRegistry,
                    toolRegistry,
                    responseProcessor,
                    consoleInterface,
                    fileReferenceService
                );

                if (!string.IsNullOrWhiteSpace(subagentPrompt))
                {
                    // Subagent mode - process the given prompt and exit
                    await agentService
                        .ProcessSubagentPromptAsync(subagentPrompt);
                }
                else
                {
                    // Normal interactive mode
                    await agentService
                        .RunAsync(globalCts.Token);
                }
            }
            catch (OperationCanceledException) when (globalCts.Token.IsCancellationRequested)
            {
                WriteLine("\nApplication shutdown completed.");
            }
            catch (Exception ex)
            {
                WriteError($"Fatal error: {ex.Message}");
            }
            finally
            {
                globalCts?.Dispose();
            }
        }
    }
}