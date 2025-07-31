import React, { useState, useEffect, useCallback } from 'react';
import { Box, Text, useInput, useApp, Static } from 'ink';
import { BackendClient } from './backend-client.js';
import { MessageHandler } from './message-handler.js';
import { UIState, DisplayMessage, OutputMessage, ModelOption, ChallengeOption, ApiKeyOption, HelpOption, FileSuggestion } from './types.js';
import path from 'path';

type Props = {
  workspacePath?: string;
};

const initialUIState: UIState = {
  messages: [],
  currentInput: '',
  sessionState: null,
  isThinking: false,
  isProgress: false,
  progressMessage: '',
  awaitingApproval: null,
  commandHistory: [],
  historyIndex: -1,
  currentMode: {
    mode: 0,
    displayName: 'DEFAULT',
    color: 'cyan',
  },
  modelSelector: {
    isVisible: false,
    options: [],
    currentModel: undefined,
  },
  challengeSelector: {
    isVisible: false,
    options: [],
  },
  apiKeySelector: {
    isVisible: false,
    options: [],
  },
  helpSelector: {
    isVisible: false,
    options: [],
  },
  fileSuggestions: {
    isVisible: false,
    suggestions: [],
    searchTerm: '',
    cursorPosition: 0,
    atSymbolPosition: 0,
  },
};

export default function App({ workspacePath }: Props) {
  const [uiState, setUIState] = useState<UIState>(initialUIState);
  const [backendClient, setBackendClient] = useState<BackendClient | null>(null);
  const [messageHandler, setMessageHandler] = useState<MessageHandler | null>(null);
  const [isInitializing, setIsInitializing] = useState(true);
  const [initError, setInitError] = useState<string | null>(null);

  // File suggestion state management
  const fileSuggestionTimeoutRef = React.useRef<NodeJS.Timeout | null>(null);

  const { exit } = useApp();

  // Initialize backend client and message handler
  useEffect(() => {
    const initializeBackend = async () => {
      try {
        // Determine workspace path (go up two levels from frontend/duo-code-frontend to reach the .NET project root)
        const targetWorkspacePath = workspacePath || path.resolve(process.cwd(), '../../');
        
        const client = new BackendClient(targetWorkspacePath);
        const handler = new MessageHandler(setUIState);

        // Set up event handlers
        client.on('message', (message: OutputMessage) => {
          handler.handleMessage(message);
        });

        client.on('error', (error: Error) => {
          setUIState(state => ({
            ...state,
            messages: [...state.messages, {
              id: `error_${Date.now()}`,
              type: 'error',
              content: `Backend Error: ${error.message}`,
              style: 'error',
              timestamp: new Date(),
            }],
          }));
        });

        client.on('stderr', (data: string) => {
          setUIState(state => ({
            ...state,
            messages: [...state.messages, {
              id: `stderr_${Date.now()}`,
              type: 'stderr',
              content: `[Backend] ${data}`,
              style: 'dim',
              timestamp: new Date(),
            }],
          }));
        });

        client.on('exit', ({ code, signal }) => {
          setUIState(state => ({
            ...state,
            messages: [...state.messages, {
              id: `exit_${Date.now()}`,
              type: 'info',
              content: `Backend process exited with code ${code} and signal ${signal}`,
              style: 'warning',
              timestamp: new Date(),
            }],
          }));
        });

        // Start the backend process
        await client.start();
        await client.initialize();

        setBackendClient(client);
        setMessageHandler(handler);
        setIsInitializing(false);

        // Add welcome message
        setUIState(state => ({
          ...state,
          messages: [{
            id: 'welcome',
            type: 'info',
            content: '🚀 Duo-Code Terminal Frontend\nConnected to .NET backend in headless mode.\nType your message or use commands like /help, /exit, /clear, etc.',
            style: 'info',
            timestamp: new Date(),
          }],
        }));

      } catch (error) {
        setInitError(error instanceof Error ? error.message : 'Unknown error');
        setIsInitializing(false);
      }
    };

    initializeBackend();

    // Cleanup on unmount
    return () => {
      if (backendClient) {
        backendClient.shutdown();
      }
      if (fileSuggestionTimeoutRef.current) {
        clearTimeout(fileSuggestionTimeoutRef.current);
      }
    };
  }, [workspacePath]);

  // Selector states
  const [selectedModelIndex, setSelectedModelIndex] = useState(0);
  const [selectedChallengeIndex, setSelectedChallengeIndex] = useState(0);
  const [selectedApiKeyIndex, setSelectedApiKeyIndex] = useState(0);
  const [selectedHelpIndex, setSelectedHelpIndex] = useState(0);
  const [selectedFileSuggestionIndex, setSelectedFileSuggestionIndex] = useState(0);

  // Handle model selection
  const handleModelSelect = useCallback((option: ModelOption) => {
    if (!backendClient || !messageHandler) return;
    
    // Send command to backend
    backendClient.sendCommand('model', [option.provider, option.model]);
    
    // Hide the selector
    messageHandler.hideModelSelector();
    
    // Add user message to display
    messageHandler.addUserMessage(`> /model ${option.provider} ${option.model}`);
  }, [backendClient, messageHandler]);

  // Handle challenge selection
  const handleChallengeSelect = useCallback((option: ChallengeOption) => {
    if (!backendClient || !messageHandler) return;
    
    // Send command to backend - could be run, info, etc.
    backendClient.sendCommand('challenge', ['info', option.id]);
    
    // Hide the selector
    messageHandler.hideChallengeSelector();
    
    // Add user message to display
    messageHandler.addUserMessage(`> /challenge info ${option.id}`);
  }, [backendClient, messageHandler]);

  // Handle API key selection
  const handleApiKeySelect = useCallback((option: ApiKeyOption) => {
    if (!backendClient || !messageHandler) return;
    
    // Send command to backend
    backendClient.sendCommand('apikey', [option.action]);
    
    // Hide the selector
    messageHandler.hideApiKeySelector();
    
    // Add user message to display
    messageHandler.addUserMessage(`> /apikey ${option.action}`);
  }, [backendClient, messageHandler]);

  // Handle help selection
  const handleHelpSelect = useCallback((option: HelpOption) => {
    if (!backendClient || !messageHandler) return;
    
    // For help, we could show detailed help for the specific command
    // For now, just show that command was selected
    messageHandler.hideHelpSelector();
    messageHandler.addUserMessage(`> Selected help for: ${option.name} (${option.type})`);
    
    // Optionally, show more detailed help for this specific command
    if (option.type === 'Action') {
      backendClient.sendCommand(option.name, []);
    } else {
      // For prompt commands, maybe show usage info
      const message = `Prompt command "${option.name}" - submit to AI assistant for execution`;
      setUIState(state => ({
        ...state,
        messages: [...state.messages, {
          id: Date.now().toString(),
          type: 'info',
          content: message,
          timestamp: new Date()
        }]
      }));
    }
  }, [backendClient, messageHandler]);

  // Handle file suggestion selection
  const handleFileSuggestionSelect = useCallback((suggestion: FileSuggestion) => {
    if (!messageHandler) return;
    
    // Update input by replacing from @ onwards with @filename
    setUIState(state => {
      const input = state.currentInput;
      const lastAtIndex = input.lastIndexOf('@');
      
      if (lastAtIndex !== -1) {
        const beforeAt = input.substring(0, lastAtIndex);
        const newInput = beforeAt + '@' + suggestion.fileName;
        return {
          ...state,
          currentInput: newInput,
        };
      }
      return state;
    });
    
    // Hide file suggestions
    messageHandler.hideFileSuggestions();
  }, [messageHandler]);



  // Update selected indices when selectors become visible
  useEffect(() => {
    if (uiState.modelSelector.isVisible) {
      const currentIndex = uiState.modelSelector.options.findIndex(opt => opt.isCurrent);
      setSelectedModelIndex(Math.max(0, currentIndex));
    }
  }, [uiState.modelSelector.isVisible, uiState.modelSelector.options]);

  useEffect(() => {
    if (uiState.challengeSelector.isVisible) {
      setSelectedChallengeIndex(0);
    }
  }, [uiState.challengeSelector.isVisible]);

  useEffect(() => {
    if (uiState.apiKeySelector.isVisible) {
      setSelectedApiKeyIndex(0);
    }
  }, [uiState.apiKeySelector.isVisible]);

  useEffect(() => {
    if (uiState.helpSelector.isVisible) {
      setSelectedHelpIndex(0);
    }
  }, [uiState.helpSelector.isVisible]);

  useEffect(() => {
    if (uiState.fileSuggestions.isVisible) {
      setSelectedFileSuggestionIndex(0);
    }
  }, [uiState.fileSuggestions.isVisible]);

  // Simple file suggestion trigger - separate from input handling
  useEffect(() => {
    if (!messageHandler || !backendClient) return;
    
    const input = uiState.currentInput;
    const lastAtIndex = input.lastIndexOf('@');
    
    if (lastAtIndex !== -1) {
      const afterAt = input.substring(lastAtIndex + 1);
      
      // Only show suggestions if:
      // 1. No spaces after @ (still typing filename)
      // 2. The text after @ doesn't look like a complete filename followed by text
      // 3. We're at the end of the input or immediately after the @
      const hasSpaceAfterAt = afterAt.includes(' ');
      
      if (!hasSpaceAfterAt) {
        if (!uiState.fileSuggestions.isVisible) {
          // Show empty suggestions immediately
          messageHandler.showFileSuggestions(afterAt, input.length, lastAtIndex);
        }
        
        // Simple timeout to request files (no complex debouncing)
        const timeoutId = setTimeout(() => {
          if (backendClient) {
            backendClient.sendFileSuggestionRequest(afterAt, input.length, input);
          }
        }, 300);
        
        return () => clearTimeout(timeoutId);
      } else if (uiState.fileSuggestions.isVisible) {
        // Has space after @, hide suggestions
        messageHandler.hideFileSuggestions();
      }
    } else if (uiState.fileSuggestions.isVisible) {
      // No @ in input, hide suggestions
      messageHandler.hideFileSuggestions();
    }
    
    // Return undefined for other cases
    return undefined;
  }, [uiState.currentInput]); // Only depend on currentInput

  // Handle keyboard input
  useInput((input, key) => {
    if (isInitializing || initError) return;

    // Determine which selector is active (exclude file suggestions)
    const activeSelector = uiState.modelSelector.isVisible ? 'model' :
                          uiState.challengeSelector.isVisible ? 'challenge' :
                          uiState.apiKeySelector.isVisible ? 'apikey' :
                          uiState.helpSelector.isVisible ? 'help' : null;

    // Handle selector navigation
    if (activeSelector) {
      if (key.escape) {
        messageHandler?.hideAllSelectors();
        return;
      }
      
      if (key.upArrow || key.downArrow) {
        const isUp = key.upArrow;
        
        switch (activeSelector) {
          case 'model':
            setSelectedModelIndex(prev => 
              isUp ? (prev > 0 ? prev - 1 : uiState.modelSelector.options.length - 1)
                   : (prev < uiState.modelSelector.options.length - 1 ? prev + 1 : 0)
            );
            break;
          case 'challenge':
            setSelectedChallengeIndex(prev => 
              isUp ? (prev > 0 ? prev - 1 : uiState.challengeSelector.options.length - 1)
                   : (prev < uiState.challengeSelector.options.length - 1 ? prev + 1 : 0)
            );
            break;
          case 'apikey':
            setSelectedApiKeyIndex(prev => 
              isUp ? (prev > 0 ? prev - 1 : uiState.apiKeySelector.options.length - 1)
                   : (prev < uiState.apiKeySelector.options.length - 1 ? prev + 1 : 0)
            );
            break;
          case 'help':
            setSelectedHelpIndex(prev => 
              isUp ? (prev > 0 ? prev - 1 : uiState.helpSelector.options.length - 1)
                   : (prev < uiState.helpSelector.options.length - 1 ? prev + 1 : 0)
            );
            break;
        }
        return;
      }
      
      if (key.return) {
        switch (activeSelector) {
          case 'model':
            const selectedModel = uiState.modelSelector.options[selectedModelIndex];
            if (selectedModel) handleModelSelect(selectedModel);
            break;
          case 'challenge':
            const selectedChallenge = uiState.challengeSelector.options[selectedChallengeIndex];
            if (selectedChallenge) handleChallengeSelect(selectedChallenge);
            break;
          case 'apikey':
            const selectedApiKey = uiState.apiKeySelector.options[selectedApiKeyIndex];
            if (selectedApiKey) handleApiKeySelect(selectedApiKey);
            break;
          case 'help':
            const selectedHelp = uiState.helpSelector.options[selectedHelpIndex];
            if (selectedHelp) handleHelpSelect(selectedHelp);
            break;
        }
        return;
      }
      
      // Don't handle other inputs when any selector is visible
      return;
    }

    // Handle file suggestions separately (only when no other selector is active)
    if (uiState.fileSuggestions.isVisible) {
      if (key.escape) {
        messageHandler?.hideFileSuggestions();
        return;
      }
      
      if (key.upArrow || key.downArrow) {
        const isUp = key.upArrow;
        setSelectedFileSuggestionIndex(prev => 
          isUp ? (prev > 0 ? prev - 1 : uiState.fileSuggestions.suggestions.length - 1)
               : (prev < uiState.fileSuggestions.suggestions.length - 1 ? prev + 1 : 0)
        );
        return;
      }
      
      if (key.return) {
        const selectedFile = uiState.fileSuggestions.suggestions[selectedFileSuggestionIndex];
        if (selectedFile) {
          handleFileSuggestionSelect(selectedFile);
        }
        return;
      }
    }

    if (key.escape) {
      // Cancel current operation
      if (backendClient && uiState.awaitingApproval) {
        backendClient.cancelOperation();
        messageHandler?.clearApproval();
      }
      return;
    }

    if (key.ctrl && input.toLowerCase() === 'c') {
      // Graceful exit
      if (backendClient) {
        backendClient.shutdown().then(() => exit());
      } else {
        exit();
      }
      return;
    }

    if (key.return) {
      // Handle different input modes
      if (uiState.awaitingApproval) {
        handleApprovalInput();
      } else {
        handleUserInput();
      }
      return;
    }

    if (key.upArrow) {
      navigateHistory('up');
      return;
    }

    if (key.downArrow) {
      navigateHistory('down');
      return;
    }

    if (key.backspace || key.delete) {
      // ONLY handle input, nothing else
      setUIState(state => ({
        ...state,
        currentInput: state.currentInput.slice(0, -1),
      }));
      return;
    }

    if (key.ctrl && input.toLowerCase() === 'l') {
      // Clear screen (equivalent to /clear command)
      if (backendClient) {
        backendClient.sendCommand('clear');
      }
      return;
    }

    if (key.ctrl && input.toLowerCase() === 'm') {
      // Mode switch
      if (backendClient) {
        backendClient.sendModeSwitch();
      }
      return;
    }

    // Regular character input - ONLY handle input, nothing else
    if (input && !key.ctrl && !key.meta) {
      setUIState(state => ({
        ...state,
        currentInput: state.currentInput + input,
      }));
    }
  });

  const handleUserInput = useCallback(() => {
    if (!backendClient || !messageHandler || !uiState.currentInput.trim()) return;

    const input = uiState.currentInput.trim();
    
    // Add user message to display
    messageHandler.addUserMessage(`> ${input}`);

    // Clear input and hide file suggestions
    setUIState(state => ({
      ...state,
      currentInput: '',
      historyIndex: -1,
    }));
    
    if (messageHandler) {
      messageHandler.hideFileSuggestions();
    }

    // Send to backend
    if (input.startsWith('/')) {
      // Handle commands
      const parts = input.slice(1).split(' ');
      const command = parts[0];
      const args = parts.slice(1);
      backendClient.sendCommand(command || '', args);
    } else {
      // Regular user input
      backendClient.sendUserInput(input);
    }
  }, [backendClient, messageHandler, uiState.currentInput]);

  const handleApprovalInput = useCallback(() => {
    if (!backendClient || !messageHandler || !uiState.awaitingApproval) return;

    const input = uiState.currentInput.trim().toLowerCase();
    const validOptions = uiState.awaitingApproval.data.options.map(opt => opt.toLowerCase());

    if (validOptions.includes(input)) {
      backendClient.sendApprovalResponse(input as 'yes' | 'always' | 'no');
      messageHandler.clearApproval();
      setUIState(state => ({
        ...state,
        currentInput: '',
      }));
    } else {
      // Invalid input - show error
      messageHandler.addUserMessage(`Invalid option. Please choose from: ${uiState.awaitingApproval.data.options.join(', ')}`);
      setUIState(state => ({
        ...state,
        currentInput: '',
      }));
    }
  }, [backendClient, messageHandler, uiState.awaitingApproval, uiState.currentInput]);

  const navigateHistory = useCallback((direction: 'up' | 'down') => {
    const history = uiState.commandHistory;
    if (history.length === 0) return;

    let newIndex = uiState.historyIndex;
    
    if (direction === 'up') {
      newIndex = newIndex === -1 ? history.length - 1 : Math.max(0, newIndex - 1);
    } else {
      newIndex = newIndex === -1 ? -1 : newIndex + 1;
      if (newIndex >= history.length) newIndex = -1;
    }

    const newInput = newIndex === -1 ? '' : history[newIndex] || '';

    setUIState(state => ({
      ...state,
      currentInput: newInput,
      historyIndex: newIndex,
    }));
  }, [uiState.commandHistory, uiState.historyIndex]);

  // Render loading state
  if (isInitializing) {
    return (
      <Box flexDirection="column">
        <Text color="yellow">🔄 Initializing Duo-Code backend...</Text>
        <Text color="gray">Starting .NET process in headless mode...</Text>
      </Box>
    );
  }

  // Render error state
  if (initError) {
    return (
      <Box flexDirection="column">
        <Text color="red">❌ Failed to initialize backend:</Text>
        <Text color="red">{initError}</Text>
        <Text color="gray">Make sure you're running from the correct directory and .NET is installed.</Text>
        <Text color="gray">Press Ctrl+C to exit.</Text>
      </Box>
    );
  }

  return (
    <Box flexDirection="column" height="100%">
      {/* Header with mode and session info */}
      <Box paddingX={1} paddingY={0}>
        <Box justifyContent="space-between" width="100%">
          <Box>
            {uiState.sessionState && (
              <Text color="gray">Model: {uiState.sessionState.currentModel}</Text>
            )}
          </Box>
          <Box>
            <Text color={uiState.currentMode.color} dimColor>
              {uiState.currentMode.displayName.toLowerCase()}
            </Text>
          </Box>
        </Box>
      </Box>

      {/* Messages area */}
      <Box flexGrow={1} flexDirection="column" paddingX={2} paddingY={1}>
        <Static items={uiState.messages}>
          {(message: DisplayMessage) => (
            <Box key={message.id} marginBottom={0}>
              <MessageComponent message={message} />
            </Box>
          )}
        </Static>

        {/* Interactive Selectors */}
        {uiState.modelSelector.isVisible && (
          <Box flexDirection="column" borderStyle="round" borderColor="cyan" paddingX={2} paddingY={1} marginY={1}>
            <Text color="cyan" bold>🤖 Select AI Model</Text>
            <Box flexDirection="column" marginTop={1}>
              {uiState.modelSelector.options.map((option, index) => (
                <Box key={option.value}>
                  <Text color={index === selectedModelIndex ? 'cyan' : 'gray'} backgroundColor={index === selectedModelIndex ? 'cyan' : undefined} bold={index === selectedModelIndex}>
                    {index === selectedModelIndex ? ' ▶ ' : '   '}
                    {option.label}
                  </Text>
                </Box>
              ))}
            </Box>
          </Box>
        )}

        {uiState.challengeSelector.isVisible && (
          <Box flexDirection="column" borderStyle="round" borderColor="yellow" paddingX={2} paddingY={1} marginY={1}>
            <Text color="yellow" bold>🎯 Select Challenge</Text>
            <Box flexDirection="column" marginTop={1}>
              {uiState.challengeSelector.options.map((option, index) => (
                <Box key={option.value}>
                  <Text color={index === selectedChallengeIndex ? 'black' : 'gray'} backgroundColor={index === selectedChallengeIndex ? 'yellow' : undefined} bold={index === selectedChallengeIndex}>
                    {index === selectedChallengeIndex ? ' ▶ ' : '   '}
                    {option.label}
                  </Text>
                </Box>
              ))}
            </Box>
          </Box>
        )}

        {uiState.apiKeySelector.isVisible && (
          <Box flexDirection="column" borderStyle="round" borderColor="green" paddingX={2} paddingY={1} marginY={1}>
            <Text color="green" bold>🔑 API Key Management</Text>
            <Box flexDirection="column" marginTop={1}>
              {uiState.apiKeySelector.options.map((option, index) => (
                <Box key={option.value}>
                  <Text color={index === selectedApiKeyIndex ? 'black' : 'gray'} backgroundColor={index === selectedApiKeyIndex ? 'green' : undefined} bold={index === selectedApiKeyIndex}>
                    {index === selectedApiKeyIndex ? ' ▶ ' : '   '}
                    {option.label}
                  </Text>
                </Box>
              ))}
            </Box>
          </Box>
        )}

        {uiState.helpSelector.isVisible && (
          <Box flexDirection="column" borderStyle="round" borderColor="magenta" paddingX={2} paddingY={1} marginY={1}>
            <Text color="magenta" bold>📚 Command Help</Text>
            <Box flexDirection="column" marginTop={1}>
              {uiState.helpSelector.options.map((option, index) => (
                <Box key={option.value}>
                  <Text color={index === selectedHelpIndex ? 'black' : 'gray'} backgroundColor={index === selectedHelpIndex ? 'magenta' : undefined} bold={index === selectedHelpIndex}>
                    {index === selectedHelpIndex ? ' ▶ ' : '   '}
                    {option.label}
                  </Text>
                </Box>
              ))}
            </Box>
          </Box>
        )}


        {/* Status indicators */}
        {uiState.isThinking && (
          <Box borderStyle="round" borderColor="yellow" paddingX={2} paddingY={0} marginY={1}>
            <Text color="yellow">💭 Thinking...</Text>
          </Box>
        )}

        {uiState.isProgress && (
          <Box borderStyle="round" borderColor="blue" paddingX={2} paddingY={0} marginY={1}>
            <Text color="blue">⏳ {uiState.progressMessage}</Text>
          </Box>
        )}
      </Box>

      {/* Input area */}
      <Box borderStyle="single" borderColor="gray" paddingX={1}>
        {uiState.awaitingApproval ? (
          <Box flexDirection="column">
            <Box>
              <Text color="yellow" bold>⚠️  Approval Required </Text>
              <Text color="gray">({uiState.awaitingApproval.data.options.join(' / ')})</Text>
            </Box>
            <Box>
              <Text color="yellow">approval{'>'}  </Text>
              <Text>{uiState.currentInput}</Text>
              <Text backgroundColor="white" color="black"> </Text>
            </Box>
          </Box>
        ) : (
          <Box>
            <Text color="green">{'>'} </Text>
            <Text>{uiState.currentInput}</Text>
            <Text backgroundColor="white" color="black"> </Text>
          </Box>
        )}
      </Box>

      {/* File Suggestions - With selection highlighting */}
      {uiState.fileSuggestions.isVisible && (
        <Box paddingX={1} paddingY={0}>
          {uiState.fileSuggestions.suggestions.length > 0 ? (
            <Box flexDirection="column">
              {uiState.fileSuggestions.suggestions.slice(0, 5).map((suggestion, index) => (
                <Box key={suggestion.relativePath}>
                  <Text color={index === selectedFileSuggestionIndex ? 'black' : 'gray'} backgroundColor={index === selectedFileSuggestionIndex ? 'gray' : undefined} bold={index === selectedFileSuggestionIndex}>
                    {index === selectedFileSuggestionIndex ? ' ▶ ' : '   '}
                    {suggestion.fileName}
                    {suggestion.directory && <Text dimColor> ({suggestion.directory})</Text>}
                  </Text>
                </Box>
              ))}
            </Box>
          ) : (
            <Text color="gray" dimColor>Loading files...</Text>
          )}
        </Box>
      )}
    </Box>
  );
}

// Component to render individual messages with appropriate styling
function MessageComponent({ message }: { message: DisplayMessage }) {
  const getColor = (style: string): string => {
    switch (style) {
      case 'error':
        return 'red';
      case 'warning':
        return 'yellow';
      case 'success':
        return 'green';
      case 'info':
        return 'blue';
      case 'assistant':
        return 'cyan';
      case 'user':
        return 'white';
      case 'tool':
        return 'magenta';
      case 'dim':
        return 'gray';
      default:
        return 'white';
    }
  };

  // Add visual separator for different message types
  const getPrefix = (type: string, style?: string): string => {
    if (type === 'error' || style === 'error') return '❌ ';
    if (type === 'info' || style === 'info') return 'ℹ️  ';
    if (type === 'success' || style === 'success') return '✅ ';
    if (type === 'warning' || style === 'warning') return '⚠️  ';
    if (style === 'assistant') return '🤖 ';
    if (style === 'tool') return '🔧 ';
    if (style === 'user') return '› ';
    return '';
  };

	return (
    <Box flexDirection="column" marginBottom={message.style === 'assistant' || message.style === 'tool' ? 1 : 0}>
      <Box>
        <Text color={getColor(message.style || 'normal')}>
          {getPrefix(message.type, message.style)}{message.content}
		</Text>
      </Box>
    </Box>
	);
}
