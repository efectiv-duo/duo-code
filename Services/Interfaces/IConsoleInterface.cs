using duo_code.Models;

namespace duo_code.Services.Interfaces
{
    public interface IConsoleInterface
    {
        void ShowThinking();
        void ClearThinking();
        void ShowInfo(string info);
        void ShowError(string error);
        void ShowProgress(string description, Action action);
        Task ShowProgressAsync(string description, Func<Task> asyncAction);
    }
}