namespace duo_code.Models;

public class ChatCompletionChunk
{
    public string Id { get; set; }
    public List<Choice> Choices { get; set; }
    public int Created { get; set; }
    public string Model { get; set; }
    public string SystemFingerprint { get; set; }
    public string Object { get; set; }

    public class Choice
    {
        public Delta Delta { get; set; }
        public int Index { get; set; }
    }

    public class Delta
    {
        public string Content { get; set; }
    }
}