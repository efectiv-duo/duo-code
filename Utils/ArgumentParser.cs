using System;
using System.Linq;

namespace duo_code.Utils
{
    public static class ArgumentParser
    {
        public static string? ParseSubagentPrompt(string[] args)
        {
            if (!args.Contains("--subagent"))
                return null;
                
            // Find the prompt argument (everything after --subagent)
            var subagentIndex = Array.IndexOf(args, "--subagent");
            if (subagentIndex >= 0 && subagentIndex < args.Length - 1)
            {
                return args[subagentIndex + 1];
            }
            
            return null;
        }
    }
}