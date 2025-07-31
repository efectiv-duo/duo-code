# Duo-Code Headless Backend API Documentation

## Overview

The Duo-Code backend can run in headless mode for integration with custom frontends (like React-Ink). In headless mode, the backend communicates via JSON messages over stdin/stdout instead of using the console interface.

## Starting Headless Mode

```bash
dotnet run --headless
```

## Communication Protocol

- **Input**: Send JSON messages to stdin (one per line)
- **Output**: Receive JSON messages from stdout (one per line)
- **Errors**: stderr contains .NET runtime errors and diagnostics

## Message Format

All messages follow this structure:

```typescript
interface InputMessage {
  type: string;
  data?: object;
  id?: string;  // Optional correlation ID
}

interface OutputMessage {
  type: string;
  data?: object;
  id?: string;  // Correlation ID from input (if provided)
  sessionState?: HeadlessSessionState;
}
```

## Input Message Types

### 1. Initialize Session
```json
{
  "type": "init",
  "data": {},
  "id": "init1"
}
```

### 2. User Input
```json
{
  "type": "user_input",
  "data": {
    "content": "Hello, how are you?"
  },
  "id": "msg1"
}
```

### 3. Command Execution
```json
{
  "type": "command",
  "data": {
    "command": "help",
    "args": []
  },
  "id": "cmd1"
}
```

### 4. Mode Switch
```json
{
  "type": "mode_switch",
  "data": {},
  "id": "mode1"
}
```
*Cycles through: Default → Planning → Orchestrator → Default*

### 5. File Suggestion Request
```json
{
  "type": "file_suggestion_request",
  "data": {
    "searchTerm": "Program",
    "cursorPosition": 10,
    "currentLine": "Look at @Program"
  },
  "id": "suggest1"
}
```

### 6. History Navigation
```json
{
  "type": "history_navigation",
  "data": {
    "direction": "up"  // "up" or "down"
  },
  "id": "hist1"
}
```

### 7. Approval Response
```json
{
  "type": "approval_response",
  "data": {
    "response": "yes",  // "yes", "always", "no"
    "feedback": "Looks good to me"
  },
  "id": "approval1"
}
```

### 8. Cancel Operation
```json
{
  "type": "cancel_operation",
  "data": {},
  "id": "cancel1"
}
```

## Output Message Types

### 1. Text Output
```json
{
  "type": "text",
  "data": {
    "content": "Welcome to Duo-Code!",
    "style": "info"  // "normal", "info", "warning", "success"
  },
  "id": null
}
```

### 2. Assistant Response
```json
{
  "type": "assistant_response",
  "data": {
    "content": "I can help you with that task.",
    "thinking": "Let me think about this...",  // Optional
    "isComplete": true
  },
  "id": "msg1"
}
```

### 3. Tool Execution
```json
{
  "type": "tool_execution",
  "data": {
    "toolName": "READ_FILE",
    "description": "Executing READ_FILE..."
  },
  "id": null
}
```

### 4. Tool Result
```json
{
  "type": "tool_result",
  "data": {
    "toolName": "READ_FILE",
    "result": "File contents here...",
    "success": true
  },
  "id": null
}
```

### 5. Error
```json
{
  "type": "error",
  "data": {
    "message": "Command not found",
    "details": "The command 'invalid' was not recognized",
    "errorType": "command_error"
  },
  "id": "cmd1"
}
```

### 6. File Suggestions
```json
{
  "type": "file_suggestions",
  "data": {
    "suggestions": [
      {
        "fileName": "Program.cs",
        "relativePath": "Program.cs",
        "directory": ""
      },
      {
        "fileName": "ProgramConfig.cs",
        "relativePath": "Services/ProgramConfig.cs", 
        "directory": "Services"
      }
    ],
    "totalCount": 2,
    "pageIndex": 0,
    "pageSize": 10,
    "hasMore": false,
    "searchTerm": "Program"
  },
  "id": "suggest1"
}
```

### 7. Mode Changed
```json
{
  "type": "mode_changed",
  "data": {
    "newMode": 1,  // 0=Default, 1=Planning, 2=Orchestrator
    "displayName": "PLANNING",
    "color": "yellow"  // "cyan", "yellow", "magenta"
  },
  "id": "mode1"
}
```

### 8. Awaiting Approval
```json
{
  "type": "awaiting_approval",
  "data": {
    "prompt": "Do you want to proceed with this change?",
    "options": ["yes", "always", "no"],
    "context": "This will modify 3 files"
  },
  "id": null
}
```

### 9. Progress Indicator
```json
{
  "type": "progress",
  "data": {
    "description": "Waiting for AI response...",
    "isActive": true,  // false when complete
    "percentComplete": null  // Optional progress percentage
  },
  "id": null
}
```

### 10. Thinking Indicator
```json
{
  "type": "thinking",
  "data": {
    "isActive": true,  // false when thinking is complete
    "content": null    // Optional thinking content
  },
  "id": null
}
```

### 11. Session State
```json
{
  "type": "session_state",
  "data": {
    "currentMode": 0,  // 0=Default, 1=Planning, 2=Orchestrator
    "commandHistory": ["hello", "/help", "analyze this project"],
    "isRunning": true,
    "currentModel": "qwen-3-235b-a22b",
    "approvedThisSession": false,
    "messageCount": 5
  },
  "id": "msg1"
}
```

## Session State

The session state is automatically sent after most operations and contains:

```typescript
interface HeadlessSessionState {
  currentMode: number;        // 0=Default, 1=Planning, 2=Orchestrator  
  commandHistory: string[];   // Recent commands for history navigation
  isRunning: boolean;         // false when session should terminate
  currentModel: string;       // Current AI model being used
  approvedThisSession: boolean; // Approval state for workflows
  messageCount: number;       // Total messages in conversation
}
```

## Typical Interaction Flow

1. **Frontend starts backend**: `dotnet run --headless`
2. **Initialize**: Send `init` message
3. **Backend responds**: Welcome messages + initial session state
4. **User interaction**: Send `user_input` with user's message
5. **Backend processes**: May send progress, thinking, tool execution messages
6. **Tool results**: Backend executes tools and sends results
7. **Session continues**: Repeat from step 4

## File Reference Support

The backend supports `@filename` syntax for file references:
- `@Program.cs` - Reference entire file
- `@Program.cs:10` - Reference line 10
- `@Program.cs:10-20` - Reference lines 10-20

When processing user input with file references:
1. Backend extracts file content
2. Adds it as system message to AI context
3. Replaces `@filename` with simple filename in user message

## Error Handling

- **Invalid JSON**: Backend ignores malformed input
- **Unknown message types**: Backend sends error response
- **Command failures**: Sent as error messages with details
- **API failures**: Sent as error messages
- **Cancellation**: Use `cancel_operation` message type

## Available Commands

All existing CLI commands work in headless mode:

- `/help` - Show available commands
- `/exit` - Terminate session  
- `/clear` - Clear conversation history
- `/model` - Switch AI models
- `/stats` - Show usage statistics
- `/challenge` - Start coding challenges

## Integration Example

```javascript
const { spawn } = require('child_process');

const backend = spawn('dotnet', ['run', '--headless']);

// Send messages
function sendMessage(type, data, id) {
  const message = { type, data, id };
  backend.stdin.write(JSON.stringify(message) + '\n');
}

// Receive messages  
backend.stdout.on('data', (data) => {
  const lines = data.toString().split('\n');
  lines.forEach(line => {
    if (line.trim()) {
      const message = JSON.parse(line);
      handleMessage(message);
    }
  });
});

// Initialize
sendMessage('init', {}, 'init');

// Send user input
sendMessage('user_input', { content: 'Hello!' }, 'msg1');
```

## Development Notes

- The backend maintains full compatibility with the original console mode
- All AI logic, conversation state, and tool execution remains in .NET
- Frontend is responsible only for UI rendering and user input handling
- Session state is preserved throughout the interaction
- File suggestions work with fuzzy matching and support paging