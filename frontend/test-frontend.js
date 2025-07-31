#!/usr/bin/env node

// Simple test script to run the frontend
import { spawn } from 'child_process';
import path from 'path';

console.log('🚀 Starting Duo-Code Terminal Frontend...');
console.log('📁 Working directory:', process.cwd());

// Get the workspace path (../ from frontend)
const workspacePath = path.resolve(process.cwd(), '../');
console.log('🎯 Workspace path:', workspacePath);

// Run the frontend
const child = spawn('node', ['dist/cli.js', '--workspace', workspacePath], {
  stdio: 'inherit',
  cwd: process.cwd()
});

child.on('error', (error) => {
  console.error('❌ Error starting frontend:', error.message);
  process.exit(1);
});

child.on('exit', (code, signal) => {
  console.log(`\n✨ Frontend exited with code ${code} and signal ${signal}`);
  process.exit(code || 0);
});

// Handle Ctrl+C gracefully
process.on('SIGINT', () => {
  console.log('\n🛑 Shutting down frontend...');
  child.kill('SIGINT');
});