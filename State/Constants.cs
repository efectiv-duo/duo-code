namespace duo_code.State;

public static class Constants
{
    public const string LOCAL_CONFIG_DIRECTORY_NAME = ".duocode";
    public const string GLOBAL_CONFIG_DIRECTORY_NAME = ".duocode";

    public const string LOCAL_CONFIG_FILE_NAME = "duo-code.settings.json";
    public const string MODEL_CONFIG_FILE_NAME = "model_settings.json";

    public static string GlobalConfigDirectory
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                GLOBAL_CONFIG_DIRECTORY_NAME);

            // Ensure directory exists
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            return dir;
        }
    }

    public static string LocalConfigDirectory
    {
        get
        {
            var dir = Path.Combine(
                Directory.GetCurrentDirectory(),
                LOCAL_CONFIG_DIRECTORY_NAME);

            // Ensure directory exists
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            return dir;
        }
    }
}