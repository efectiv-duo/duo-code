using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace duo_code.Models;

public class CodebaseContext
{
    public List<FileMetadata> ImportantFiles { get; set; } = new List<FileMetadata>();
    public List<string> Directories { get; set; } = new List<string>();
    public int FileCount { get; set; }
    
    public string ToJson()
    {
        var options = new JsonSerializerOptions 
        { 
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        return JsonSerializer.Serialize(this, options);
    }
}