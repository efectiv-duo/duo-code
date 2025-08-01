using duo_code.Commands.Core;
using duo_code.Models;
using duo_code.Tools.Core;
using duo_code.Services.Interfaces;
using Spectre.Console;

namespace duo_code.Services
{
    public class AgentService
    {
        private IApiService _apiService;
        private ApiProvider _currentProvider;
        private readonly CommandRegistry _commandRegistry;
        private readonly ToolRegistry _toolRegistry;
        private readonly StreamingResponseService _responseProcessor;
        private readonly SpectreConsoleInterface _console;
        private readonly ConversationState _state;
        private readonly ConversationLogger _logger;
        private readonly IFileReferenceService _fileReferenceService;

        public AgentService(
            CommandRegistry commandRegistry,
            ToolRegistry toolRegistry,
            StreamingResponseService responseProcessor,
            SpectreConsoleInterface console,
            ConversationState state,
            IFileReferenceService fileReferenceService = null)
        {
            _currentProvider = ApiSettings.CurrentProvider;
            _apiService = ApiServiceFactory.CreateApiService(_currentProvider);
            _commandRegistry = commandRegistry;
            _toolRegistry = toolRegistry;
            _responseProcessor = responseProcessor;
            _console = console;
            _state = state;
            _logger = new ConversationLogger();
            _fileReferenceService = fileReferenceService ?? new FileReferenceService();
        }

        public async Task ProcessSubagentPromptAsync(string prompt)
        {
            // Process the prompt directly (starter messages built dynamically)
            await ProcessUserMessageAsync(prompt);
        }

        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            _console.ShowWelcomeMessage();

            while (_state.IsRunning && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var (input, modeSwitch) = await _console.GetUserInputAsync(_state.CurrentMode);
                        
                    // Handle mode switch
                    if (modeSwitch.HasValue)
                    {
                        _state.CurrentMode = modeSwitch.Value;
                        continue;
                    }
                        
                    if (string.IsNullOrWhiteSpace(input)) continue;

                    if (input.StartsWith('/'))
                    {
                        await HandleCommandAsync(input);
                    }
                    else
                    {
                        await ProcessUserMessageAsync(input);
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
                await ProcessUserMessageAsync(result.PromptToSubmit);
            }

            if (result.ShouldExit)
            {
                _state.IsRunning = false;
            }
        }

        private async Task ProcessUserMessageAsync(string userInput)
        {
            // Process file references in user input
            var fileReferenceResult = await _fileReferenceService.ProcessFileReferencesAsync(userInput);
            
            // Add user message
            _state.Messages.Add(new StateMessage { Role = "user", Content = fileReferenceResult.ProcessedUserContent });
            
            // Add system message with file references if any exist
            if (fileReferenceResult.HasFileReferences && !string.IsNullOrEmpty(fileReferenceResult.SystemMessageContent))
            {
                _state.Messages.Add(new StateMessage { Role = "system", Content = fileReferenceResult.SystemMessageContent });
            }

            // Continue prompting until FINISH_TASK is received or max operations reached
            bool taskCompleted = false;
            int operationCount = 0;
            const int maxOperations = 10;
            
            while (!taskCompleted && operationCount < maxOperations)
            {
                operationCount++;
                var messageHistory = BuildMessageHistory();

                _logger.SaveConversation(messageHistory);

                using var cts = new CancellationTokenSource();
                _console.SetupCancellation(cts);

                try
                {
                    // Refresh API service if provider changed
                    RefreshApiServiceIfNeeded();
                    
                    // Convert to CerebrasMessage format
                    var cerebrasMessages = messageHistory.Select(m => new CerebrasMessage
                    {
                        Role = m.Role ?? "user",
                        Content = m.Content
                    }).ToList();

                    await AnsiConsole.Status()
                        .Spinner(Spinner.Known.Flip)
                        .SpinnerStyle(Style.Parse("green bold"))
                        .StartAsync("Waiting for a response...", async ctx =>
                        {
                            var processedResponse = await _apiService.GetAISuggestionAsync(cerebrasMessages, cts.Token, ApiSettings.CurrentModel, _console);

                            if (!string.IsNullOrWhiteSpace(processedResponse?.Content))
                            {
                                taskCompleted = await ProcessAssistantResponseAsync(processedResponse);
                            }
                        });
                }
                catch (OperationCanceledException)
                {
                    _console.ShowInfo("\nOperation cancelled.");
                    break;
                }
            }
        }

        private Task<bool> ProcessAssistantResponseAsync(ProcessedResponse response)
        {
            return Task.Run(async () =>
            {
                var tools = ToolFactory.Parse(response.Content);
                bool finishTaskFound = false;

                var message = new StateMessage
                {
                    Role = "assistant",
                    Content = response.Content,
                    Thinking = response.Thinking,
                    Actions = new List<IToolAction>()
                };

                foreach (var tool in tools)
                {
                    _console.ShowToolExecution(tool.ToolName);

                    // Check if this is the FINISH_TASK tool
                    if (tool.ToolName == "FINISH_TASK")
                    {
                        finishTaskFound = true;
                    }

                    try
                    {
                        var result = tool.Execute(Directory.GetCurrentDirectory());
                        _console.ShowToolResult(result);
                        tool.FullResult = result;
                        message.Actions.Add(tool);
                    }
                    catch (Exception ex)
                    {
                        var errorResult = $"Error: {ex.Message}";
                        _console.ShowError(errorResult);
                        tool.FullResult = errorResult;
                        message.Actions.Add(tool);
                    }
                }

                _state.Messages.Add(message);

                await Task.Delay(50);
                
                // Add tool results as a user message if there were any tools executed
                if (message.Actions != null && message.Actions.Count > 0)
                {
                    var toolResultsMessage = new StateMessage
                    {
                        Role = "user",
                        Content = message.BuildToolResultsMessage(),
                        Actions = message.Actions
                    };
                    _state.Messages.Add(toolResultsMessage);
                }
                else
                {
                    _console.ShowAssistantResponse(response.Content);

                    finishTaskFound = true;
                }
                
                // Save conversation after each assistant response
                _logger.SaveConversation(_state.Messages);
                
                // Wait for user input before continuing (unless task is finished)
                //if (!finishTaskFound)
                //{
                //    var shouldContinue = _console.WaitForContinueOrCancel();
                //    if (!shouldContinue)
                //    {
                //        return true; // Exit the loop as if task was completed
                //    }
                //}
                
                return finishTaskFound;
            });
        }

        private List<StateMessage> BuildMessageHistory()
        {
            var history = new List<StateMessage>();
            
            // Add dynamic starter messages first
            var starterMessages = BuildDynamicStarterMessages();
            history.AddRange(starterMessages);
            
            // Add user conversation messages
            var messageCount = _state.Messages.Count;
            for (int i = 0; i < messageCount; i++)
            {
                var message = _state.Messages[i];
                var distanceFromHead = messageCount - i - 1;

                history.Add(new StateMessage
                {
                    Role = message.Role,
                    Content = message.GetContentForHistory(distanceFromHead, messageCount)
                });
            }

            return history;
        }

        private List<StateMessage> BuildDynamicStarterMessages()
        {
            var contextBuilder = new ContextBuilder();
            var contextFactory = new CodebaseContextFactory(Directory.GetCurrentDirectory());

            var starterMessages = new List<StateMessage>
            {
                new StateMessage
                {
                    Role = "system",
                    Content = contextBuilder.BuildContext(_state.CurrentMode)
                },
                new StateMessage
                {
                    Role = "user",
                    Content = "Analyze the current directory context."
                },
                new StateMessage
                {
                    Role = "assistant",
                    Content = "STARTER_CONTEXT: ."
                }
            };

            var context = contextFactory.CreateContext();
            starterMessages.Add(new StateMessage
            {
                Role = "user",
                Content = context.ToJson()
            });

            starterMessages.Add(new StateMessage
            {
                Role = "assistant",
                Content = @"FINISH_TASK:
Current directory context analyzed."
            });

            return starterMessages;
        }

        private void RefreshApiServiceIfNeeded()
        {
            if (_currentProvider != ApiSettings.CurrentProvider)
            {
                _apiService?.Dispose();
                _currentProvider = ApiSettings.CurrentProvider;
                _apiService = ApiServiceFactory.CreateApiService(_currentProvider);
            }
        }
    }
}