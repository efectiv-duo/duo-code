using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using duo_code.Models;

namespace duo_code.Services
{
    public class ConversationLogger
    {
        private readonly string _logsDirectory;

        public ConversationLogger()
        {
            _logsDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".duo-code",
                "logs"
            );
            
            Directory.CreateDirectory(_logsDirectory);
        }

        public void SaveConversation(object conversation)
        {
            if (conversation == null)
                return;

            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                var fileName = $"conversation_{timestamp}.json";
                var filePath = Path.Combine(_logsDirectory, fileName);

                var json = JsonSerializer.Serialize(conversation, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                // Log silently fails to not interrupt the user experience
                Console.Error.WriteLine($"Failed to save conversation log: {ex.Message}");
            }
        }

        public string GetLogsDirectory()
        {
            return _logsDirectory;
        }
    }
}