using System.Text.Json;
using duo_code.Commands.Core;
using duo_code.Models;
using duo_code.Tools.Core;
using duo_code.Services.Interfaces;

namespace duo_code.Services
{
    public class HeadlessAgentService
    {
        private IApiService _apiService;
        private ApiProvider _currentProvider;
        private readonly CommandRegistry _commandRegistry;
        private readonly ToolRegistry _toolRegistry;
        private readonly StreamingResponseService _responseProcessor;
        private readonly HeadlessInterface _interface;
        private readonly ConversationState _state;
        private readonly ConversationLogger _logger;
        private readonly IFileReferenceService _fileReferenceService;

        // Pending approval state
        private TaskCompletionSource<ApprovalResponseData>? _pendingApproval;
        private CancellationTokenSource? _currentOperationCts;

        public HeadlessAgentService(
            CommandRegistry commandRegistry,
            ToolRegistry toolRegistry,
            StreamingResponseService responseProcessor,
            HeadlessInterface headlessInterface,
            ConversationState state,
            IFileReferenceService fileReferenceService = null)
        {
            _currentProvider = ApiSettings.CurrentProvider;
            _apiService = ApiServiceFactory.CreateApiService(_currentProvider);
            _commandRegistry = commandRegistry;
            _toolRegistry = toolRegistry;
            _responseProcessor = responseProcessor;
            _interface = headlessInterface;
            _state = state;
            _logger = new ConversationLogger();
            _fileReferenceService = fileReferenceService ?? new FileReferenceService();
            
            _interface.SetCommandRegistry(commandRegistry);
        }

        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            _interface.ShowWelcomeMessage();
            _interface.SendSessionState(_state);

            var inputBuffer = new List<string>();

            while (_state.IsRunning && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Read input from stdin
                    var inputLine = await ReadInputLineAsync(cancellationToken);
                    if (inputLine == null) continue;

                    // Parse JSON input
                    var inputMessage = JsonSerializer.Deserialize<InputMessage>(inputLine, new JsonSerializerOptions 
                    { 
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
                    });

                    if (inputMessage == null) continue;

                    await ProcessInputMessage(inputMessage);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    _interface.ShowInfo("Application shutdown requested.");
                    break;
                }
                catch (JsonException ex)
                {
                    _interface.ShowError($"Invalid JSON input: {ex.Message}");
                }
                catch (Exception ex)
                {
                    _interface.ShowError($"Error: {ex.Message}");
                }
            }
        }

        private async Task<string?> ReadInputLineAsync(CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                try
                {
                    return Console.ReadLine();
                }
                catch
                {
                    return null;
                }
            }, cancellationToken);
        }

        private async Task ProcessInputMessage(InputMessage inputMessage)
        {
            switch (inputMessage.Type)
            {
                case MessageTypes.Init:
                    await HandleInitMessage();
                    break;

                case MessageTypes.UserInput:
                    var userData = DeserializeMessageData<UserInputData>(inputMessage.Data);
                    if (userData != null && !string.IsNullOrWhiteSpace(userData.Content))
                    {
                        _interface.AddToHistory(userData.Content);
                        await ProcessUserMessageAsync(userData.Content);
                    }
                    break;

                case MessageTypes.Command:
                    var commandData = DeserializeMessageData<CommandData>(inputMessage.Data);
                    if (commandData != null)
                    {
                        await HandleCommandAsync($"/{commandData.Command}", commandData.Args);
                    }
                    break;

                case MessageTypes.ModeSwitch:
                    HandleModeSwitch();
                    break;

                case MessageTypes.FileSuggestionRequest:
                    var suggestionData = DeserializeMessageData<FileSuggestionRequestData>(inputMessage.Data);
                    if (suggestionData != null)
                    {
                        // Handle file suggestions asynchronously to avoid blocking
                        _ = Task.Run(async () => await HandleFileSuggestionRequestAsync(suggestionData, inputMessage.Id));
                    }
                    break;

                case MessageTypes.HistoryNavigation:
                    var historyData = DeserializeMessageData<HistoryNavigationData>(inputMessage.Data);
                    if (historyData != null)
                    {
                        HandleHistoryNavigation(historyData, inputMessage.Id);
                    }
                    break;

                case MessageTypes.ApprovalResponse:
                    var approvalData = DeserializeMessageData<ApprovalResponseData>(inputMessage.Data);
                    if (approvalData != null)
                    {
                        HandleApprovalResponse(approvalData);
                    }
                    break;

                case MessageTypes.CancelOperation:
                    HandleCancelOperation();
                    break;

                default:
                    _interface.ShowError($"Unknown message type: {inputMessage.Type}");
                    break;
            }

            // Send updated session state after most operations
            _interface.SendSessionState(_state, inputMessage.Id);
        }

        private async Task HandleInitMessage()
        {
            _interface.ShowInfo("Headless agent initialized and ready.");
            _interface.SendSessionState(_state);
        }

        private void HandleModeSwitch()
        {
            var nextMode = _state.CurrentMode switch
            {
                Mode.Default => Mode.Planning,
                Mode.Planning => Mode.Orchestrator,
                Mode.Orchestrator => Mode.Default,
                _ => Mode.Default
            };

            _state.CurrentMode = nextMode;
            _interface.ShowModeSwitch(nextMode);
        }

        private async Task HandleFileSuggestionRequestAsync(FileSuggestionRequestData data, string? correlationId)
        {
            try
            {
                // Get suggestions asynchronously to avoid blocking
                var suggestions = await Task.Run(() => _interface.GetFileSuggestions(data.SearchTerm, 10));
                
                var responseData = new FileSuggestionsData
                {
                    Suggestions = suggestions,
                    TotalCount = suggestions.Count,
                    PageIndex = 0,
                    PageSize = 10,
                    HasMore = false,
                    SearchTerm = data.SearchTerm
                };
                
                var message = new OutputMessage
                {
                    Type = MessageTypes.FileSuggestions,
                    Data = responseData,
                    Id = correlationId
                };
                
                // Send response back to frontend
                var json = JsonSerializer.Serialize(message, new JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
                });
                Console.WriteLine(json);
            }
            catch (Exception ex)
            {
                // Send error response if file suggestion fails
                var errorMessage = new OutputMessage
                {
                    Type = MessageTypes.Error,
                    Data = new ErrorData 
                    { 
                        Message = "Failed to get file suggestions", 
                        Details = ex.Message,
                        ErrorType = "file_suggestion_error"
                    },
                    Id = correlationId
                };
                
                var json = JsonSerializer.Serialize(errorMessage, new JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
                });
                Console.WriteLine(json);
            }
        }

        private void HandleHistoryNavigation(HistoryNavigationData data, string? correlationId)
        {
            var historyItem = _interface.GetHistoryItem(data.Direction);
            
            var responseData = new TextOutputData
            {
                Content = historyItem ?? "",
                Style = "history"
            };

            var message = new OutputMessage
            {
                Type = MessageTypes.Text,
                Data = responseData,
                Id = correlationId
            };

            var json = JsonSerializer.Serialize(message, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            });
            Console.WriteLine(json);
        }

        private void HandleApprovalResponse(ApprovalResponseData data)
        {
            if (_pendingApproval != null)
            {
                _pendingApproval.SetResult(data);
                _pendingApproval = null;
            }
        }

        private void HandleCancelOperation()
        {
            _currentOperationCts?.Cancel();
            _interface.ShowInfo("Operation cancelled by user.");
        }

        private async Task HandleCommandAsync(string input, string[]? args = null)
        {
            var parts = input.Split(' ', 2);
            var commandName = parts[0].Substring(1);
            var commandArgs = args ?? (parts.Length > 1 ? parts[1].Split(' ') : Array.Empty<string>());

            var command = _commandRegistry.GetCommand(commandName);
            if (command == null)
            {
                _interface.ShowError($"Unknown command: {commandName}");
                return;
            }

            var result = await command.ExecuteAsync(commandArgs);

            if (!result.Success)
            {
                _interface.ShowError(result.Message);
            }
            else if (!string.IsNullOrEmpty(result.Message))
            {
                _interface.ShowInfo(result.Message);
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
            _state.Messages.Add(new Message { Role = "user", Content = fileReferenceResult.ProcessedUserContent });
            
            // Add system message with file references if any exist
            if (fileReferenceResult.HasFileReferences && !string.IsNullOrEmpty(fileReferenceResult.SystemMessageContent))
            {
                _state.Messages.Add(new Message { Role = "system", Content = fileReferenceResult.SystemMessageContent });
            }

            // Continue prompting until FINISH_TASK is received
            bool taskCompleted = false;
            while (!taskCompleted)
            {
                var messageHistory = BuildMessageHistory();
                _logger.SaveConversation(messageHistory);

                _currentOperationCts = new CancellationTokenSource();

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

                    await _interface.ShowProgressAsync("Waiting for AI response...", async () =>
                    {
                        var processedResponse = await _apiService.GetAISuggestionAsync(cerebrasMessages, _currentOperationCts.Token, ApiSettings.CurrentModel, _interface);

                        if (!string.IsNullOrWhiteSpace(processedResponse?.Content))
                        {
                            taskCompleted = await ProcessAssistantResponseAsync(processedResponse);
                        }
                    });
                }
                catch (OperationCanceledException)
                {
                    _interface.ShowInfo("Operation cancelled.");
                    break;
                }
                finally
                {
                    _currentOperationCts?.Dispose();
                    _currentOperationCts = null;
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
                    _interface.ShowToolExecution(tool.ToolName);

                    // Check if this is the FINISH_TASK tool
                    if (tool.ToolName == "FINISH_TASK")
                    {
                        finishTaskFound = true;
                    }

                    try
                    {
                        var result = tool.Execute(Directory.GetCurrentDirectory());
                        _interface.ShowToolResult(result);
                        tool.FullResult = result;
                        message.Actions.Add(tool);
                    }
                    catch (Exception ex)
                    {
                        var errorResult = $"Error: {ex.Message}";
                        _interface.ShowError(errorResult);
                        tool.FullResult = errorResult;
                        message.Actions.Add(tool);
                    }
                }

                _state.Messages.Add(message);
                
                // Add tool results as a user message if there were any tools executed
                if (message.Actions != null && message.Actions.Count > 0)
                {
                    var toolResultsMessage = new Message
                    {
                        Role = "user",
                        Content = message.BuildToolResultsMessage(),
                        Actions = message.Actions
                    };
                    _state.Messages.Add(toolResultsMessage);
                }
                else
                {
                    _interface.ShowAssistantResponse(response.Content);
                    finishTaskFound = true;
                }
                
                // Save conversation after each assistant response
                _logger.SaveConversation(_state.Messages);
                
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
            var messageCount = _state.Messages.Count;
            for (int i = 0; i < messageCount; i++)
            {
                var message = _state.Messages[i];
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
                    Content = contextBuilder.BuildContext(_state.CurrentMode)
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
            if (_currentProvider != ApiSettings.CurrentProvider)
            {
                _apiService?.Dispose();
                _currentProvider = ApiSettings.CurrentProvider;
                _apiService = ApiServiceFactory.CreateApiService(_currentProvider);
            }
        }

        // Method to wait for approval responses (used by WaitForContinueOrCancel equivalent)
        private async Task<ApprovalResponseData> WaitForApprovalAsync(AwaitingApprovalData approvalData)
        {
            _pendingApproval = new TaskCompletionSource<ApprovalResponseData>();
            
            // Send the approval request to frontend
            var message = new OutputMessage
            {
                Type = MessageTypes.AwaitingApproval,
                Data = approvalData
            };

            var json = JsonSerializer.Serialize(message, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            });
            Console.WriteLine(json);

            // Wait for response
            return await _pendingApproval.Task;
        }

        private T? DeserializeMessageData<T>(object? data) where T : class
        {
            if (data == null)
                return null;

            try
            {
                // If data is already a JsonElement, serialize it back to string first
                if (data is System.Text.Json.JsonElement jsonElement)
                {
                    var jsonString = jsonElement.GetRawText();
                    return JsonSerializer.Deserialize<T>(jsonString, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                }

                // If data is a string, deserialize directly
                if (data is string stringData)
                {
                    return JsonSerializer.Deserialize<T>(stringData, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                }

                // Try to serialize the object to JSON and then deserialize as T
                var serializedData = JsonSerializer.Serialize(data, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                return JsonSerializer.Deserialize<T>(serializedData, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
            }
            catch (JsonException ex)
            {
                _interface.ShowError($"Failed to deserialize message data: {ex.Message}");
                return null;
            }
        }
    }
}