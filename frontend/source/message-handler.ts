import {
  OutputMessage,
  TextOutputMessage,
  AssistantResponseMessage,
  ToolExecutionMessage,
  ToolResultMessage,
  ErrorMessage,
  ModeChangedMessage,
  AwaitingApprovalMessage,
  ProgressMessage,
  ThinkingMessage,
  SessionStateMessage,
  FileSuggestionsMessage,
  UIState,
  DisplayMessage,
  HeadlessSessionState,
  ModelOption,
  ChallengeOption,
  ApiKeyOption,
  HelpOption,
} from './types.js';

export class MessageHandler {
  private uiStateCallback: (updater: (state: UIState) => UIState) => void;

  constructor(uiStateCallback: (updater: (state: UIState) => UIState) => void) {
    this.uiStateCallback = uiStateCallback;
  }

  handleMessage(message: OutputMessage): void {
    switch (message.type) {
      case 'text':
        this.handleTextMessage(message as TextOutputMessage);
        break;
      case 'assistant_response':
        this.handleAssistantResponse(message as AssistantResponseMessage);
        break;
      case 'tool_execution':
        this.handleToolExecution(message as ToolExecutionMessage);
        break;
      case 'tool_result':
        this.handleToolResult(message as ToolResultMessage);
        break;
      case 'error':
        this.handleError(message as ErrorMessage);
        break;
      case 'mode_changed':
        this.handleModeChanged(message as ModeChangedMessage);
        break;
      case 'awaiting_approval':
        this.handleAwaitingApproval(message as AwaitingApprovalMessage);
        break;
      case 'progress':
        this.handleProgress(message as ProgressMessage);
        break;
      case 'thinking':
        this.handleThinking(message as ThinkingMessage);
        break;
      case 'session_state':
        this.handleSessionState(message as SessionStateMessage);
        break;
      case 'file_suggestions':
        this.handleFileSuggestions(message as FileSuggestionsMessage);
        break;
      default:
        // Handle unknown message types by adding them as generic messages
        this.addDisplayMessage('info', `Unknown message type: ${message.type}`);
        break;
    }

    // Update session state if provided
    if (message.sessionState) {
      this.updateSessionState(message.sessionState);
    }
  }

  private handleTextMessage(message: TextOutputMessage): void {
    const style = this.mapStyleToColor(message.data.style || 'normal');
    
    // Check for different types of interactive responses
    if (this.isModelListResponse(message.data.content)) {
      this.handleModelListResponse(message.data.content);
    } else if (this.isChallengeListResponse(message.data.content)) {
      this.handleChallengeListResponse(message.data.content);
    } else if (this.isApiKeyMenuResponse(message.data.content)) {
      this.handleApiKeyMenuResponse(message.data.content);
    } else if (this.isHelpMenuResponse(message.data.content)) {
      this.handleHelpMenuResponse(message.data.content);
    } else {
      this.addDisplayMessage(style, message.data.content);
    }
  }

  private handleAssistantResponse(message: AssistantResponseMessage): void {
    // Show thinking if provided
    if (message.data.thinking) {
      this.addDisplayMessage('dim', `[Thinking] ${message.data.thinking}`);
    }

    // Show the assistant response
    this.addDisplayMessage('assistant', message.data.content);

    // Clear thinking indicator if response is complete
    if (message.data.isComplete) {
      this.uiStateCallback((state) => ({
        ...state,
        isThinking: false,
      }));
    }
  }

  private handleToolExecution(message: ToolExecutionMessage): void {
    this.addDisplayMessage('tool', `🔧 ${message.data.description}`);
  }

  private handleToolResult(message: ToolResultMessage): void {
    const icon = message.data.success ? '✅' : '❌';
    const style = message.data.success ? 'success' : 'error';
    this.addDisplayMessage(style, `${icon} ${message.data.toolName}: ${message.data.result}`);
  }

  private handleError(message: ErrorMessage): void {
    let errorText = `❌ Error: ${message.data.message}`;
    if (message.data.details) {
      errorText += `\n   Details: ${message.data.details}`;
    }
    this.addDisplayMessage('error', errorText);
  }

  private handleModeChanged(message: ModeChangedMessage): void {
    this.uiStateCallback((state) => ({
      ...state,
      currentMode: {
        mode: message.data.newMode,
        displayName: message.data.displayName,
        color: message.data.color,
      },
    }));

    this.addDisplayMessage('info', `🔄 Mode changed to: ${message.data.displayName}`);
  }

  private handleAwaitingApproval(message: AwaitingApprovalMessage): void {
    this.uiStateCallback((state) => ({
      ...state,
      awaitingApproval: message,
    }));

    let approvalText = `⚠️  ${message.data.prompt}`;
    if (message.data.context) {
      approvalText += `\n   Context: ${message.data.context}`;
    }
    approvalText += `\n   Options: ${message.data.options.join(', ')}`;
    
    this.addDisplayMessage('warning', approvalText);
  }

  private handleProgress(message: ProgressMessage): void {
    this.uiStateCallback((state) => ({
      ...state,
      isProgress: message.data.isActive,
      progressMessage: message.data.description,
    }));

    if (message.data.isActive) {
      let progressText = `⏳ ${message.data.description}`;
      if (message.data.percentComplete !== undefined && message.data.percentComplete !== null) {
        progressText += ` (${message.data.percentComplete}%)`;
      }
      this.addDisplayMessage('info', progressText);
    }
  }

  private handleThinking(message: ThinkingMessage): void {
    this.uiStateCallback((state) => ({
      ...state,
      isThinking: message.data.isActive,
    }));

    if (message.data.isActive && message.data.content) {
      this.addDisplayMessage('dim', `💭 ${message.data.content}`);
    }
  }

  private handleSessionState(message: SessionStateMessage): void {
    this.updateSessionState(message.data);
  }

  private updateSessionState(sessionState: HeadlessSessionState): void {
    this.uiStateCallback((state) => ({
      ...state,
      sessionState,
      commandHistory: sessionState.commandHistory,
    }));
  }

  private addDisplayMessage(type: string, content: string): void {
    const displayMessage: DisplayMessage = {
      id: this.generateMessageId(),
      type,
      content,
      style: type,
      timestamp: new Date(),
    };

    this.uiStateCallback((state) => ({
      ...state,
      messages: [...state.messages, displayMessage],
    }));
  }

  private mapStyleToColor(style: string): string {
    switch (style) {
      case 'info':
        return 'info';
      case 'warning':
        return 'warning';
      case 'success':
        return 'success';
      case 'normal':
      default:
        return 'normal';
    }
  }

  private generateMessageId(): string {
    return `ui_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
  }

  // Helper method to clear approval state
  clearApproval(): void {
    this.uiStateCallback((state) => ({
      ...state,
      awaitingApproval: null,
    }));
  }

  // Helper method to add user input to display
  addUserMessage(content: string): void {
    this.addDisplayMessage('user', content);
  }

  // Check if the content is a model list response
  private isModelListResponse(content: string): boolean {
    return content.includes('Current Model:') && 
           content.includes('Available Models:') && 
           content.includes('Usage: /model');
  }

  // Parse model list response and show interactive selector
  private handleModelListResponse(content: string): void {
    const lines = content.split('\n');
    const models: ModelOption[] = [];
    let currentModel = '';
    let inModelList = false;

    for (const line of lines) {
      if (line.startsWith('Current Model:')) {
        // Extract current model: "Current Model: Cerebras - llama-3.3-70b"
        const match = line.match(/Current Model:\s*(\w+)\s*-\s*(.+)/);
        if (match) {
          currentModel = `${match[1]}-${match[2]}`;
        }
      } else if (line.includes('Available Models:')) {
        inModelList = true;
      } else if (inModelList && line.startsWith('- ')) {
        // Parse model line: "- Cerebras - llama-3.3-70b (current)" or "- Gemini - gemini-1.5-pro"
        const match = line.match(/^-\s*(\w+)\s*-\s*([^\s]+)(\s*\(current\))?/);
        if (match) {
          const provider = match[1];
          const model = match[2];
          const isCurrent = !!match[3];
          
          // Ensure provider and model are defined
          if (provider && model) {
            const value = `${provider}-${model}`;
            
            models.push({
              label: `${provider} - ${model}${isCurrent ? ' (current)' : ''}`,
              value,
              provider,
              model,
              isCurrent
            });
          }
        }
      } else if (line.startsWith('Usage:')) {
        inModelList = false;
      }
    }

    // Show the model selector instead of adding the message
    this.uiStateCallback((state) => ({
      ...state,
      modelSelector: {
        isVisible: true,
        options: models,
        currentModel
      }
    }));
  }

  // Helper method to hide model selector
  hideModelSelector(): void {
    this.uiStateCallback((state) => ({
      ...state,
      modelSelector: {
        isVisible: false,
        options: [],
        currentModel: undefined
      }
    }));
  }

  // Challenge selector methods
  private isChallengeListResponse(content: string): boolean {
    return content.includes('🎯 Select a Challenge:') && 
           content.includes('Current Model: Challenge Selector') && 
           content.includes('Available Models:');
  }

  private handleChallengeListResponse(content: string): void {
    const lines = content.split('\n');
    const challenges: ChallengeOption[] = [];
    let inChallengeList = false;

    for (const line of lines) {
      if (line.includes('Available Models:')) {
        inChallengeList = true;
      } else if (inChallengeList && line.startsWith('- Challenge - ')) {
        // Parse challenge line: "- Challenge - 001-DebugAndFix [85/100] (✅)"
        const match = line.match(/^-\s*Challenge\s*-\s*(\d+)-([^\s\[]+).*?\(([^)]+)\)/);
        if (match) {
          const id = match[1];
          const name = match[2];
          const status = match[3];
          
          if (id && name && status) {
            challenges.push({
              label: `Challenge #${id} - ${name} (${status})`,
              value: `Challenge-${id}`,
              id,
              name,
              status
            });
          }
        }
      } else if (line.startsWith('Usage:')) {
        inChallengeList = false;
      }
    }

    this.uiStateCallback((state) => ({
      ...state,
      challengeSelector: {
        isVisible: true,
        options: challenges
      }
    }));
  }

  hideChallengeSelector(): void {
    this.uiStateCallback((state) => ({
      ...state,
      challengeSelector: {
        isVisible: false,
        options: []
      }
    }));
  }

  // API Key selector methods
  private isApiKeyMenuResponse(content: string): boolean {
    return content.includes('🔑 API Key Management:') && 
           content.includes('Current Model: API Key Manager') && 
           content.includes('Available Models:');
  }

  private handleApiKeyMenuResponse(content: string): void {
    const lines = content.split('\n');
    const actions: ApiKeyOption[] = [];
    let inActionList = false;

    for (const line of lines) {
      if (line.includes('Available Models:')) {
        inActionList = true;
      } else if (inActionList && line.startsWith('- Actions - ')) {
        // Parse action line: "- Actions - show (has-keys)" or "- Actions - reset (can-reset)"
        const match = line.match(/^-\s*Actions\s*-\s*(\w+)\s*\(([^)]+)\)/);
        if (match) {
          const action = match[1];
          const status = match[2];
          
          if (action && status) {
            actions.push({
              label: `${action} (${status})`,
              value: `Actions-${action}`,
              action,
              status
            });
          }
        }
      } else if (line.startsWith('Usage:')) {
        inActionList = false;
      }
    }

    this.uiStateCallback((state) => ({
      ...state,
      apiKeySelector: {
        isVisible: true,
        options: actions
      }
    }));
  }

  hideApiKeySelector(): void {
    this.uiStateCallback((state) => ({
      ...state,
      apiKeySelector: {
        isVisible: false,
        options: []
      }
    }));
  }

  // Help selector methods
  private isHelpMenuResponse(content: string): boolean {
    return content.includes('📚 Command Help:') && 
           content.includes('Current Model: Help Browser') && 
           content.includes('Available Models:');
  }

  private handleHelpMenuResponse(content: string): void {
    const lines = content.split('\n');
    const commands: HelpOption[] = [];
    let inCommandList = false;

    for (const line of lines) {
      if (line.includes('Available Models:')) {
        inCommandList = true;
      } else if (inCommandList && (line.startsWith('- Action - ') || line.startsWith('- Prompt - '))) {
        // Parse command line: "- Action - help (aliases: h,?)" or "- Prompt - test"
        const match = line.match(/^-\s*(Action|Prompt)\s*-\s*(\w+)(?:\s*\(aliases:\s*([^)]+)\))?/);
        if (match) {
          const type = match[1];
          const name = match[2];
          const aliases = match[3];
          
          if (type && name) {
            commands.push({
              label: `${type}: ${name}${aliases ? ` (${aliases})` : ''}`,
              value: `${type}-${name}`,
              type,
              name,
              aliases
            });
          }
        }
      } else if (line.startsWith('Usage:')) {
        inCommandList = false;
      }
    }

    this.uiStateCallback((state) => ({
      ...state,
      helpSelector: {
        isVisible: true,
        options: commands
      }
    }));
  }

  hideHelpSelector(): void {
    this.uiStateCallback((state) => ({
      ...state,
      helpSelector: {
        isVisible: false,
        options: []
      }
    }));
  }

  // File suggestions methods
  private handleFileSuggestions(message: FileSuggestionsMessage): void {
    this.uiStateCallback((state) => ({
      ...state,
      fileSuggestions: {
        ...state.fileSuggestions,
        isVisible: true,
        suggestions: message.data.suggestions,
        searchTerm: message.data.searchTerm,
      }
    }));
  }

  showFileSuggestions(searchTerm: string, cursorPosition: number, atSymbolPosition: number): void {
    this.uiStateCallback((state) => ({
      ...state,
      fileSuggestions: {
        isVisible: true,
        suggestions: [],
        searchTerm,
        cursorPosition,
        atSymbolPosition,
      }
    }));
  }

  hideFileSuggestions(): void {
    this.uiStateCallback((state) => ({
      ...state,
      fileSuggestions: {
        isVisible: false,
        suggestions: [],
        searchTerm: '',
        cursorPosition: 0,
        atSymbolPosition: 0,
      }
    }));
  }

  // Helper to hide all selectors
  hideAllSelectors(): void {
    this.uiStateCallback((state) => ({
      ...state,
      modelSelector: { isVisible: false, options: [], currentModel: undefined },
      challengeSelector: { isVisible: false, options: [] },
      apiKeySelector: { isVisible: false, options: [] },
      helpSelector: { isVisible: false, options: [] },
      fileSuggestions: { isVisible: false, suggestions: [], searchTerm: '', cursorPosition: 0, atSymbolPosition: 0 }
    }));
  }
}