using duo_code.Commands.Core;
using duo_code.Services.Interfaces;
using duo_code.Tools.Core;

namespace duo_code.Services
{
    public class CollaborativeAgentService
    {
        private readonly CommandRegistry _commandRegistry;
        private OrchestratorService _orchestratorService;
        private readonly ConsoleInterface _console;
        private IApiService _apiService;
        private ApiProvider _currentProvider;

        public CollaborativeAgentService(
            CommandRegistry commandRegistry,
            ToolRegistry toolRegistry,
            StreamingResponseService responseProcessor,
            ConsoleInterface console,
            IFileReferenceService fileReferenceService = null)
        {
            _commandRegistry = commandRegistry;
            _console = console;
            _currentProvider = CurrentState.Provider;
            _apiService = ApiServiceFactory.CreateApiService(_currentProvider);

            // Create worker service
            var workerService = new WorkerService(_apiService, toolRegistry, console);
            
            // Create orchestrator service
            _orchestratorService = new OrchestratorService(_apiService, console, workerService);
        }

        public async Task ProcessSubagentPromptAsync(string prompt)
        {
            await _orchestratorService.ProcessUserMessageAsync(prompt);
        }

        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            _console.ShowWelcomeMessage();

            while (CurrentState.IsRunning && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var (input, modeSwitch) = await _console.GetUserInputAsync(CurrentState.CurrentMode);

                    // Handle mode switch
                    if (modeSwitch.HasValue)
                    {
                        CurrentState.CurrentMode = modeSwitch.Value;
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(input)) continue;

                    if (input.StartsWith('/'))
                    {
                        await HandleCommandAsync(input);
                    }
                    else
                    {
                        // Refresh API service if provider changed
                        RefreshApiServiceIfNeeded();
                        
                        // Process through orchestrator
                        await _orchestratorService.ProcessUserMessageAsync(input, cancellationToken);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    _console.ShowInfo("Operation cancelled. Exiting...");
                    break;
                }
                catch (Exception ex)
                {
                    _console.ShowError($"Error: {ex.Message}");
                }
            }
        }

        private async Task HandleCommandAsync(string input)
        {
            var parts = input.Split(' ', 2);
            var commandName = parts[0].Substring(1);
            var args = parts.Length > 1 ? parts[1].Split(' ') : Array.Empty<string>();

            var command = _commandRegistry.GetCommand(commandName);
            if (command == null)
            {
                _console.ShowError($"Unknown command: {commandName}");
                return;
            }

            var result = await command.ExecuteAsync(args);

            if (!result.Success)
            {
                _console.ShowError(result.Message);
            }
            else if (!string.IsNullOrEmpty(result.Message))
            {
                _console.ShowInfo(result.Message);
            }

            // Handle prompt commands
            if (!string.IsNullOrEmpty(result.PromptToSubmit))
            {
                await _orchestratorService.ProcessUserMessageAsync(result.PromptToSubmit);
            }

            if (result.ShouldExit)
            {
                CurrentState.IsRunning = false;
            }
        }

        private void RefreshApiServiceIfNeeded()
        {
            if (_currentProvider != CurrentState.Provider)
            {
                _currentProvider = CurrentState.Provider;
                _apiService?.Dispose();
                _apiService = ApiServiceFactory.CreateApiService(_currentProvider);
                
                // Recreate services with new API service
                var toolRegistry = new ToolRegistry();
                var workerService = new WorkerService(_apiService, toolRegistry, _console);
                _orchestratorService = new OrchestratorService(_apiService, _console, workerService);
            }
        }
    }
}