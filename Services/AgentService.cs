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
        private readonly ConsoleInterface _console;
        private readonly ConversationLogger _logger;
        private readonly IFileReferenceService _fileReferenceService;

        public AgentService(
            CommandRegistry commandRegistry,
            ToolRegistry toolRegistry,
            StreamingResponseService responseProcessor,
            ConsoleInterface console,
            IFileReferenceService fileReferenceService = null)
        {
            _currentProvider = CurrentState.Provider;
            _apiService = ApiServiceFactory.CreateApiService(_currentProvider);
            _commandRegistry = commandRegistry;
            _toolRegistry = toolRegistry;
            _responseProcessor = responseProcessor;
            _console = console;
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
                CurrentState.IsRunning = false;
            }
        }

        private async Task ProcessUserMessageAsync(string userInput)
        {
            // Process file references in user input
            var fileReferenceResult = await _fileReferenceService.ProcessFileReferencesAsync(userInput);
            
            // Add user message
            CurrentState.Messages.Add(new Message { Role = "user", Content = fileReferenceResult.ProcessedUserContent });
            
            // Add system message with file references if any exist
            if (fileReferenceResult.HasFileReferences && !string.IsNullOrEmpty(fileReferenceResult.SystemMessageContent))
            {
                CurrentState.Messages.Add(new Message { Role = "system", Content = fileReferenceResult.SystemMessageContent });
            }

            // Continue prompting until FINISH_TASK is received
            bool taskCompleted = false;
            while (!taskCompleted)
            {
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

                    WriteInfo("Sent API request.");

                    var processedResponse = await _apiService.GetAISuggestionAsync(cerebrasMessages, cts.Token, CurrentState.Model, _console);

                    if (!string.IsNullOrWhiteSpace(processedResponse?.Content))
                    {
                        WriteInfo("Reading response.");

                        taskCompleted = await ProcessAssistantResponseAsync(processedResponse);
                    }
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
            return Task.Run(() =>
            {
                var tools = ToolFactory.Parse(response.Content);
                bool finishTaskFound = false;

                var message = new Message
                {
                    Role = "assistant",
                    Content = response.Content,
                    Thinking = response.Thinking,
                    Actions = new List<IToolAction>()
                };

                foreach (var tool in tools)
                {
                    WriteInfo($"Executing {tool.ToolName}");

                    // Check if this is the FINISH_TASK tool
                    if (tool.ToolName == "FINISH_TASK")
                    {
                        finishTaskFound = true;
                    }

                    try
                    {
                        tool.Execute(Directory.GetCurrentDirectory());

                        WriteToolResult(tool.ConsoleMessage ?? "no message");
                        
                        message.Actions.Add(tool);
                    }
                    catch (Exception ex)
                    {
                        var errorResult = $"Error: {ex.Message}";
                        _console.ShowError(errorResult);
                        tool.ResultMessage = errorResult;
                        message.Actions.Add(tool);
                    }
                }

                CurrentState.Messages.Add(message);
                
                // Add tool results as a user message if there were any tools executed
                if (message.Actions != null && message.Actions.Count > 0)
                {
                    var toolResultsMessage = new Message
                    {
                        Role = "user",
                        Content = message.BuildToolResultsMessage(),
                        Actions = message.Actions
                    };
                    CurrentState.Messages.Add(toolResultsMessage);
                }
                else
                {
                    WriteAssistantResponse(response.Content);

                    finishTaskFound = true;
                }
                
                // Save conversation after each assistant response
                _logger.SaveConversation(CurrentState.Messages);
                
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

        private List<Message> BuildMessageHistory()
        {
            var history = new List<Message>();
            
            // Add dynamic starter messages first
            var starterMessages = BuildDynamicStarterMessages();
            history.AddRange(starterMessages);
            
            // Add user conversation messages
            var messageCount = CurrentState.Messages.Count;
            for (int i = 0; i < messageCount; i++)
            {
                var message = CurrentState.Messages[i];
                var distanceFromHead = messageCount - i - 1;

                history.Add(new Message
                {
                    Role = message.Role,
                    Content = message.GetContentForHistory(distanceFromHead, messageCount)
                });
            }

            return history;
        }

        private List<Message> BuildDynamicStarterMessages()
        {
            var contextBuilder = new ContextBuilder();
            var contextFactory = new CodebaseContextFactory(Directory.GetCurrentDirectory());

            var starterMessages = new List<Message>
            {
                new Message
                {
                    Role = "system",
                    Content = contextBuilder.BuildContext(CurrentState.CurrentMode)
                },
                new Message
                {
                    Role = "user",
                    Content = "Analyze the current directory context."
                },
                new Message
                {
                    Role = "assistant",
                    Content = "STARTER_CONTEXT: ."
                }
            };

            var context = contextFactory.CreateContext();
            starterMessages.Add(new Message
            {
                Role = "user",
                Content = context.ToJson()
            });

            starterMessages.Add(new Message
            {
                Role = "assistant",
                Content = @"FINISH_TASK:
Current directory context analyzed."
            });

            return starterMessages;
        }

        private void RefreshApiServiceIfNeeded()
        {
            if (_currentProvider != CurrentState.Provider)
            {
                _apiService?.Dispose();

                _currentProvider = CurrentState.Provider;
                _apiService = ApiServiceFactory.CreateApiService(_currentProvider);
            }
        }
    }
}