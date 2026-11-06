namespace ProductionIntelligence.Infrastructure;

public sealed class AiOptions
{
    public const string SectionName = "AI";

    public string Mode { get; set; } = "Ollama";
    public string BaseUrl { get; set; } = "http://localhost:11434/v1/";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "llama3.2";
}
