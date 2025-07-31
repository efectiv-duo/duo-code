import { spawn, ChildProcess } from 'child_process';
import { EventEmitter } from 'events';
import {
  InputMessage,
  OutputMessage,
  InitMessage,
  UserInputMessage,
  CommandMessage,
  ModeSwitchMessage,
  ApprovalResponseMessage,
  CancelOperationMessage,
  HistoryNavigationMessage,
  FileSuggestionRequestMessage,
} from './types.js';

export class BackendClient extends EventEmitter {
  private process: ChildProcess | null = null;
  private isInitialized = false;
  private messageIdCounter = 0;

  constructor(private workspacePath: string) {
    super();
  }

  async start(): Promise<void> {
    return new Promise((resolve, reject) => {
      try {
        // Start the .NET backend in headless mode
        this.process = spawn('dotnet', ['run', '--headless'], {
          cwd: this.workspacePath,
          stdio: ['pipe', 'pipe', 'pipe'],
        });

        if (!this.process.stdout || !this.process.stdin || !this.process.stderr) {
          throw new Error('Failed to create backend process pipes');
        }

        // Handle stdout (JSON messages)
        let buffer = '';
        this.process.stdout.on('data', (data: Buffer) => {
          buffer += data.toString();
          const lines = buffer.split('\n');
          buffer = lines.pop() || ''; // Keep incomplete line in buffer

          for (const line of lines) {
            if (line.trim()) {
              try {
                const message: OutputMessage = JSON.parse(line);
                this.emit('message', message);
              } catch (error) {
                this.emit('error', new Error(`Failed to parse JSON: ${line}`));
              }
            }
          }
        });

        // Handle stderr (diagnostics and errors)
        this.process.stderr.on('data', (data: Buffer) => {
          const errorText = data.toString().trim();
          if (errorText) {
            this.emit('stderr', errorText);
          }
        });

        // Handle process events
        this.process.on('error', (error) => {
          this.emit('error', error);
          reject(error);
        });

        this.process.on('exit', (code, signal) => {
          this.emit('exit', { code, signal });
          this.process = null;
          this.isInitialized = false;
        });

        // Wait a moment for the process to start, then resolve
        setTimeout(() => {
          if (this.process && !this.process.killed) {
            resolve();
          } else {
            reject(new Error('Backend process failed to start'));
          }
        }, 1000);
      } catch (error) {
        reject(error);
      }
    });
  }

  async initialize(): Promise<void> {
    if (!this.process || !this.process.stdin) {
      throw new Error('Backend process not started');
    }

    const initMessage: InitMessage = {
      type: 'init',
      data: {},
      id: this.generateMessageId(),
    };

    this.sendMessage(initMessage);
    this.isInitialized = true;
  }

  sendUserInput(content: string): void {
    const message: UserInputMessage = {
      type: 'user_input',
      data: { content },
      id: this.generateMessageId(),
    };
    this.sendMessage(message);
  }

  sendCommand(command: string, args: string[] = []): void {
    const message: CommandMessage = {
      type: 'command',
      data: { command, args },
      id: this.generateMessageId(),
    };
    this.sendMessage(message);
  }

  sendModeSwitch(): void {
    const message: ModeSwitchMessage = {
      type: 'mode_switch',
      data: {},
      id: this.generateMessageId(),
    };
    this.sendMessage(message);
  }

  sendApprovalResponse(response: 'yes' | 'always' | 'no', feedback?: string): void {
    const message: ApprovalResponseMessage = {
      type: 'approval_response',
      data: { response, feedback },
      id: this.generateMessageId(),
    };
    this.sendMessage(message);
  }

  sendHistoryNavigation(direction: 'up' | 'down'): void {
    const message: HistoryNavigationMessage = {
      type: 'history_navigation',
      data: { direction },
      id: this.generateMessageId(),
    };
    this.sendMessage(message);
  }

  cancelOperation(): void {
    const message: CancelOperationMessage = {
      type: 'cancel_operation',
      data: {},
      id: this.generateMessageId(),
    };
    this.sendMessage(message);
  }

  sendFileSuggestionRequest(searchTerm: string, cursorPosition: number, currentLine: string): void {
    const message: FileSuggestionRequestMessage = {
      type: 'file_suggestion_request',
      data: { searchTerm, cursorPosition, currentLine },
      id: this.generateMessageId(),
    };
    this.sendMessage(message);
  }

  private sendMessage(message: InputMessage): void {
    if (!this.process || !this.process.stdin) {
      throw new Error('Backend process not available');
    }

    const jsonMessage = JSON.stringify(message) + '\n';
    this.process.stdin.write(jsonMessage);
  }

  private generateMessageId(): string {
    return `msg_${++this.messageIdCounter}`;
  }

  async shutdown(): Promise<void> {
    return new Promise((resolve) => {
      if (!this.process) {
        resolve();
        return;
      }

      const cleanup = () => {
        this.process = null;
        this.isInitialized = false;
        resolve();
      };

      // Try graceful shutdown first
      this.process.on('exit', cleanup);
      
      if (this.process.stdin) {
        this.process.stdin.end();
      }

      // Force kill if not closed within 5 seconds
      setTimeout(() => {
        if (this.process && !this.process.killed) {
          this.process.kill('SIGTERM');
          setTimeout(() => {
            if (this.process && !this.process.killed) {
              this.process.kill('SIGKILL');
            }
          }, 2000);
        }
      }, 5000);
    });
  }

  isRunning(): boolean {
    return this.process !== null && !this.process.killed;
  }

  getIsInitialized(): boolean {
    return this.isInitialized;
  }
}