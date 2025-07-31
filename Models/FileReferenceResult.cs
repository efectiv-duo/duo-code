namespace duo_code.Models;

public class FileReferenceResult
{
    public string ProcessedUserContent { get; set; } = string.Empty;
    public string SystemMessageContent { get; set; } = string.Empty;
    public bool HasFileReferences { get; set; }
}