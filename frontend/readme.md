# Duo-Code Terminal Frontend

A terminal-based frontend for Duo-Code built with React-Ink that communicates with the .NET backend in headless mode.

## Features

- ✅ Spawns and manages .NET backend process (`dotnet run --headless`)
- ✅ Real-time JSON message communication over stdin/stdout
- ✅ Interactive terminal UI with message history
- ✅ Command history navigation with arrow keys
- ✅ Session state display (mode, model, message count)
- ✅ Colored mode indicators (Default/Planning/Orchestrator)
- ✅ Tool execution display with success/error indicators
- ✅ Approval prompt handling
- ✅ Progress and thinking indicators
- ✅ Graceful shutdown and error handling

## Installation

```bash
npm install
```

## Usage

### Development Mode

```bash
npm run dev
```

### Production Build

```bash
npm run build
```

### Running the Frontend

From the frontend directory:
```bash
# Run from default location (../../ relative to .NET project)
node dist/cli.js

# Or specify custom workspace path
node dist/cli.js --workspace=/path/to/duo-code-project
```

## Keyboard Shortcuts

- **Enter**: Send message/command or approve prompt
- **↑/↓ Arrow Keys**: Navigate command history
- **Ctrl+C**: Graceful exit
- **Ctrl+L**: Clear screen (equivalent to `/clear`)
- **Ctrl+M**: Switch modes (Default → Planning → Orchestrator)
- **Esc**: Cancel current operation or approval prompt

## Commands

All Duo-Code CLI commands now feature interactive selectors in headless mode:
- `/help` - Interactive command browser with categorized navigation
- `/exit` - Terminate session
- `/clear` - Clear conversation history
- `/model` - Interactive AI model selector with arrow key navigation
- `/stats` - Show usage statistics
- `/challenge` - Interactive challenge browser and management
- `/apikey` - Interactive API key management with status display

### Interactive Command Interface

All major commands now provide interactive selectors when no arguments are provided:

#### Model Selection (`/model`)
1. Type `/model` to open the model selector
2. Use ↑↓ arrow keys to navigate through available models
3. Current model is highlighted and pre-selected
4. Press Enter to select a model
5. Press Esc to cancel selection

#### Challenge Management (`/challenge`)
1. Type `/challenge` to open the challenge selector
2. Browse all available challenges with status indicators (✅ completed, 🔄 in progress, ❌ not started)
3. Use ↑↓ arrow keys to navigate through challenges
4. Press Enter to view challenge details
5. See completion scores and difficulty levels
6. Press Esc to cancel selection

#### API Key Management (`/apikey`)
1. Type `/apikey` to open the API key management menu
2. View current key status (✅ exists, ❌ not found)
3. Select actions: show, reset, help
4. Use ↑↓ arrow keys to navigate options
5. Press Enter to execute action
6. Press Esc to cancel selection

#### Help Browser (`/help`)
1. Type `/help` to open the interactive help browser
2. Commands are categorized as Action (CLI-executed) or Prompt (AI-submitted)
3. View command aliases and descriptions
4. Use ↑↓ arrow keys to navigate commands
5. Press Enter to see detailed help for a specific command
6. Press Esc to cancel selection

### Universal Navigation

All interactive selectors use consistent controls:
- **↑↓ Arrow Keys**: Navigate through options
- **Enter**: Select/Execute current option
- **Esc**: Cancel and return to command input
- **Visual Feedback**: Selected items are highlighted with ❯ indicator and color coding

The frontend automatically sends the appropriate commands to the backend using the JSON protocol.

## File References

The frontend supports `@filename` syntax for file references:
- `@Program.cs` - Reference entire file
- `@Program.cs:10` - Reference line 10
- `@Program.cs:10-20` - Reference lines 10-20

## Architecture

- **BackendClient**: Manages .NET process and I/O communication
- **MessageHandler**: Routes incoming messages to UI updates
- **App Component**: Main React-Ink UI with interactive features
- **Types**: TypeScript interfaces for the headless API protocol

## Development

This project uses:
- React-Ink for terminal UI
- TypeScript for type safety
- ESM modules for modern JavaScript
- Meow for CLI argument parsing
