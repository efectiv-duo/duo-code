using duo_code.Commands.Core;
using duo_code.Services.Interfaces;
using duo_code.Tools;
using duo_code.Tools.Core;
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

            // Continue prompting until no tool request is received
            bool taskCompleted = false;
            while (!taskCompleted)
            {
                var messageHistory = BuildMessageHistory();

                using var cts = new CancellationTokenSource();
                _console.SetupCancellation(cts);

                try
                {
                    // Refresh API service if provider changed
                    RefreshApiServiceIfNeeded();

                    // Convert to CerebrasMessage format
                    var messages = messageHistory.Select(m => new CerebrasMessage
                    {
                        Role = m.Role ?? "user",
                        Content = m.Content
                    }).ToList();

                    _logger.SaveConversation(messages.Select(m => $"{m.Role.ToUpper()}:\n{m.Content}\n\n").Aggregate((a, b) => $"{a}\n{b}"));

                    WriteInfo("Sent API request.");

                    var processedResponse = await _apiService.GetAISuggestionAsync(messages, cts.Token, CurrentState.Model, _console);

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

                bool userCancelledToolExecution = false;

                var message = new Message
                {
                    Role = "assistant",
                    Content = response.Content,
                    Thinking = response.Thinking,
                    Actions = new List<IToolAction>()
                };

                CurrentState.Messages.Add(message);

                if (tools.Count == 0)
                {
                    // If no tools have to be executed, just display the assistant's response.
                    WriteAssistantResponse(response.Content);

                    return true;
                }

                foreach (var tool in tools)
                {
                    if (userCancelledToolExecution) // If user already cancelled a previous tool, skip remaining
                    {
                        tool.SkipExecution();
                        message.Actions.Add(tool);
                        continue;
                    }

                    WriteAssistantResponse($"Executing {tool.ConsoleRequestMessage}");

                    if (tool.RequiresConfirmation)
                    {
                        if (!_console.WaitForContinueOrCancel())
                        {
                            tool.CancelExecution();
                            WriteError(tool.ConsoleResultMessage);
                            message.Actions.Add(tool);
                            userCancelledToolExecution = true;
                            break;
                        }
                    }
                    try
                    {
                        tool.Execute(Directory.GetCurrentDirectory());

                        WriteToolResult(tool.ConsoleResultMessage ?? "no message");

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
                {   // If no tools were executed, just display the assistant's response.
                    WriteAssistantResponse(response.Content);
                }

                return userCancelledToolExecution; // Return true if task finished OR user cancelled tools
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

            // Execute the ListFilesAction to get actual directory structure
            var listFilesAction = new ListFilesAction
            {
                Path = ".",
                Depth = 3,
                DirectoriesOnly = false
            };
            var listResult = listFilesAction.Execute(Directory.GetCurrentDirectory());

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
                    Content = "Analyze the current directory."
                },
                new Message
                {
                    Role = "assistant",
                    Content = "LIST_FILES: . depth:3 directories_only:false",
                    Actions = new List<IToolAction> { listFilesAction }
                },
                new Message
                {
                    Role = "user",
                    Content = listResult,
                    Actions = new List<IToolAction> { listFilesAction }
                }
            };

            // Check if DUOCODE.md exists and add its contents
            var duocodeFilePath = Path.Combine(Directory.GetCurrentDirectory(), "DUOCODE.md");
            if (File.Exists(duocodeFilePath))
            {
                starterMessages.Add(new Message
                {
                    Role = "assistant",
                    Content = "READ_FILE: DUOCODE.md purpose:I want to understand the context better."
                });

                var readFileAction = new ReadFileAction
                {
                    Path = "DUOCODE.md",
                    CompressService = null,
                    Purpose = "I want to understand the context better."
                };
                var duocodeContent = readFileAction.Execute(Directory.GetCurrentDirectory());

                starterMessages.Add(new Message
                {
                    Role = "user",
                    Content = duocodeContent,
                    Actions = new List<IToolAction> { readFileAction }
                });
            }

            starterMessages.Add(new Message
            {
                Role = "assistant",
                Content = "Current directory context analyzed."
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