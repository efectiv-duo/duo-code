namespace duo_code.State;

public static class CurrentState
{
    public static ApiProvider Provider { get; set; } = ApiProvider.Cerebras;
    public static string Model { get; set; } = "qwen-3-235b-a22b";
    public static string FallbackModel { get; set; } = "qwen-3-32b";
    public static List<Message> Messages { get; } = new();
    public static bool IsRunning { get; set; } = true;
    public static Mode CurrentMode { get; set; } = Mode.Default;

    public static void UpdateModelAndProvider(ApiProvider apiProvider, string model)
    {
        Model = model;
        Provider = apiProvider;
    }

    public static readonly Dictionary<ApiProvider, List<string>> AvailableModels = new()
    {
        {
            ApiProvider.Cerebras, new List<string>
            {
                "qwen-3-235b-a22b",
                "qwen-3-32b",
                "llama-4-maverick-17b-128e-instruct",
                "llama-4-scout-17b-16e-instruct",
                "deepseek-r1-distill-llama-70b",
                "llama-3.3-70b"
            }
        },
        {
            ApiProvider.Gemini, new List<string>
            {
                "gemini-2.5-flash"
            }
        }
    };
}