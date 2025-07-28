using System.Collections.Generic;
using duo_code.Models;

namespace duo_code.Services
{
    public class ConversationState
    {
        public List<Message> Messages { get; } = new();
        public bool IsRunning { get; set; } = true;
        public string CurrentModel { get; set; } = "qwen-3-235b-a22b";
        public Mode CurrentMode { get; set; } = Mode.Default;
    }
}