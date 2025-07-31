#!/usr/bin/env node
import React from 'react';
import {render} from 'ink';
import meow from 'meow';
import App from './app.js';

const cli = meow(
	`
	Usage
	  $ duo-code-frontend

	Options
		--workspace  Path to the Duo-Code .NET project workspace (default: ../../)

	Examples
	  $ duo-code-frontend
	  $ duo-code-frontend --workspace=/path/to/duo-code
`,
	{
		importMeta: import.meta,
		flags: {
			workspace: {
				type: 'string',
				alias: 'w',
			},
		},
	},
);

render(<App workspacePath={cli.flags.workspace} />);
