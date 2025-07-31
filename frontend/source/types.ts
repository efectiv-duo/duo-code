// TypeScript interfaces for the Duo-Code Headless API Protocol

export interface InputMessage {
  type: string;
  data?: object;
  id?: string;
}

export interface OutputMessage {
  type: string;
  data?: object;
  id?: string;
  sessionState?: HeadlessSessionState;
}

export interface HeadlessSessionState {
  currentMode: number;        // 0=Default, 1=Planning, 2=Orchestrator
  commandHistory: string[];   // Recent commands for history navigation
  isRunning: boolean;         // false when session should terminate
  currentModel: string;       // Current AI model being used
  approvedThisSession: boolean; // Approval state for workflows
  messageCount: number;       // Total messages in conversation
}

// Input message types
export interface InitMessage extends InputMessage {
  type: 'init';
  data: {};
}

export interface UserInputMessage extends InputMessage {
  type: 'user_input';
  data: {
    content: string;
  };
}

export interface CommandMessage extends InputMessage {
  type: 'command';
  data: {
    command: string;
    args: string[];
  };
}

export interface ModeSwitchMessage extends InputMessage {
  type: 'mode_switch';
  data: {};
}

export interface FileSuggestionRequestMessage extends InputMessage {
  type: 'file_suggestion_request';
  data: {
    searchTerm: string;
    cursorPosition: number;
    currentLine: string;
  };
}

// File suggestion data type
export interface FileSuggestion {
  fileName: string;
  relativePath: string;
  directory: string;
}

export interface HistoryNavigationMessage extends InputMessage {
  type: 'history_navigation';
  data: {
    direction: 'up' | 'down';
  };
}

export interface ApprovalResponseMessage extends InputMessage {
  type: 'approval_response';
  data: {
    response: 'yes' | 'always' | 'no';
    feedback?: string;
  };
}

export interface CancelOperationMessage extends InputMessage {
  type: 'cancel_operation';
  data: {};
}

// Output message types
export interface TextOutputMessage extends OutputMessage {
  type: 'text';
  data: {
    content: string;
    style?: 'normal' | 'info' | 'warning' | 'success';
  };
}

export interface AssistantResponseMessage extends OutputMessage {
  type: 'assistant_response';
  data: {
    content: string;
    thinking?: string;
    isComplete: boolean;
  };
}

export interface ToolExecutionMessage extends OutputMessage {
  type: 'tool_execution';
  data: {
    toolName: string;
    description: string;
  };
}

export interface ToolResultMessage extends OutputMessage {
  type: 'tool_result';
  data: {
    toolName: string;
    result: string;
    success: boolean;
  };
}

export interface ErrorMessage extends OutputMessage {
  type: 'error';
  data: {
    message: string;
    details?: string;
    errorType?: string;
  };
}

export interface FileSuggestionsMessage extends OutputMessage {
  type: 'file_suggestions';
  data: {
    suggestions: FileSuggestion[];
    totalCount: number;
    pageIndex: number;
    pageSize: number;
    hasMore: boolean;
    searchTerm: string;
  };
}

export interface ModeChangedMessage extends OutputMessage {
  type: 'mode_changed';
  data: {
    newMode: number;
    displayName: string;
    color: 'cyan' | 'yellow' | 'magenta';
  };
}

export interface AwaitingApprovalMessage extends OutputMessage {
  type: 'awaiting_approval';
  data: {
    prompt: string;
    options: string[];
    context?: string;
  };
}

export interface ProgressMessage extends OutputMessage {
  type: 'progress';
  data: {
    description: string;
    isActive: boolean;
    percentComplete?: number;
  };
}

export interface ThinkingMessage extends OutputMessage {
  type: 'thinking';
  data: {
    isActive: boolean;
    content?: string;
  };
}

export interface SessionStateMessage extends OutputMessage {
  type: 'session_state';
  data: HeadlessSessionState;
}

// UI State types
export interface DisplayMessage {
  id: string;
  type: string;
  content: string;
  style?: string;
  timestamp: Date;
}

export interface ModelOption {
  label: string;
  value: string;
  provider: string;
  model: string;
  isCurrent: boolean;
}

export interface ChallengeOption {
  label: string;
  value: string;
  id: string;
  name: string;
  status: string;
}

export interface ApiKeyOption {
  label: string;
  value: string;
  action: string;
  status: string;
}

export interface HelpOption {
  label: string;
  value: string;
  type: string;
  name: string;
  aliases?: string;
}

export interface UIState {
  messages: DisplayMessage[];
  currentInput: string;
  sessionState: HeadlessSessionState | null;
  isThinking: boolean;
  isProgress: boolean;
  progressMessage: string;
  awaitingApproval: AwaitingApprovalMessage | null;
  commandHistory: string[];
  historyIndex: number;
  currentMode: {
    mode: number;
    displayName: string;
    color: 'cyan' | 'yellow' | 'magenta';
  };
  modelSelector: {
    isVisible: boolean;
    options: ModelOption[];
    currentModel?: string;
  };
  challengeSelector: {
    isVisible: boolean;
    options: ChallengeOption[];
  };
  apiKeySelector: {
    isVisible: boolean;
    options: ApiKeyOption[];
  };
  helpSelector: {
    isVisible: boolean;
    options: HelpOption[];
  };
  fileSuggestions: {
    isVisible: boolean;
    suggestions: FileSuggestion[];
    searchTerm: string;
    cursorPosition: number;
    atSymbolPosition: number;
  };
}