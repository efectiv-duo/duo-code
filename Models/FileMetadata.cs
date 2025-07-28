namespace duo_code.Models;

public class FileMetadata
{
    public string Name { get; set; } = "unknown";
    public List<string> FirstLines { get; set; } = new List<string>();
    public int TotalLines { get; set; }
}