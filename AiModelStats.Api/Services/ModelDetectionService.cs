using System.Text.RegularExpressions;

namespace AiModelStats.Api.Services;

public class ModelDetectionService
{
    private static readonly RegexOptions Opts = RegexOptions.IgnoreCase | RegexOptions.Compiled;

    // Each entry: display name → patterns that indicate real code/config usage.
    // Patterns are intentionally specific to avoid matching README prose that slips through.
    private static readonly Dictionary<string, Regex[]> Patterns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GPT-4"] =
        [
            new(@"model\s*[:=]\s*[""']gpt-4", Opts),           // model="gpt-4", model="gpt-4o", model="gpt-4-turbo"
            new(@"model\s*[:=]\s*[""']gpt-4o", Opts),
            new(@"openai.*ChatCompletion", Opts),
            new(@"client\.chat\.completions\.create", Opts),    // modern openai-python / openai-node SDK
            new(@"""gpt-4[\w-]*""", Opts),                      // bare string literal in config/JSON
        ],
        ["GPT-3.5"] =
        [
            new(@"model\s*[:=]\s*[""']gpt-3\.5", Opts),
            new(@"""gpt-3\.5-turbo[\w-]*""", Opts),
        ],
        ["o1 / o3"] =
        [
            new(@"model\s*[:=]\s*[""']o[13][\w-]*[""']", Opts),  // model="o1", model="o3-mini"
            new(@"""o[13]-(mini|preview|pro)""", Opts),
        ],
        ["Claude"] =
        [
            new(@"claude-(3|3-5|3\.5|sonnet|haiku|opus)", Opts),
            new(@"anthropic\.(Anthropic|messages)", Opts),        // SDK instantiation or messages.create
            new(@"AnthropicClient\(", Opts),                      // .NET SDK
        ],
        ["Gemini"] =
        [
            new(@"gemini-(pro|1\.5|2\.0|flash|ultra)", Opts),
            new(@"google\.generativeai", Opts),
            new(@"GenerativeModel\(", Opts),
            new(@"genai\.Client\(", Opts),
        ],
        ["Llama"] =
        [
            new(@"meta-llama/", Opts),
            new(@"llama-?[23]", Opts),
            new(@"meta\.llama", Opts),
        ],
        ["Mistral"] =
        [
            new(@"mistral-(7b|large|medium|small|nemo)", Opts),
            new(@"mistralai/(Mistral|Mixtral)", Opts),
            new(@"MistralClient\(", Opts),
        ],
        ["Whisper"] =
        [
            new(@"openai\.audio\.transcriptions", Opts),
            new(@"model\s*[:=]\s*[""']whisper", Opts),
            new(@"pipeline\([""']automatic-speech-recognition", Opts),  // transformers
        ],
        ["DALL-E"] =
        [
            new(@"openai\.images\.", Opts),
            new(@"model\s*[:=]\s*[""']dall-e", Opts),
        ],
        ["Stable Diffusion"] =
        [
            new(@"StableDiffusionPipeline", Opts),
            new(@"from diffusers", Opts),
            new(@"stability-?ai/stable-diffusion", Opts),
        ],
    };

    public Dictionary<string, int> Detect(string fileContent)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in fileContent.AsSpan().EnumerateLines())
        {
            var trimmed = line.TrimStart();

            // Skip comment-only lines — these are documentation, not real usage.
            if (trimmed.StartsWith("//", StringComparison.Ordinal) ||
                trimmed.StartsWith("#", StringComparison.Ordinal) ||
                trimmed.StartsWith("*", StringComparison.Ordinal) ||
                trimmed.StartsWith("<!--", StringComparison.Ordinal))
                continue;

            var lineStr = line.ToString();
            foreach (var (model, regexes) in Patterns)
            {
                if (regexes.Any(r => r.IsMatch(lineStr)))
                    result[model] = result.GetValueOrDefault(model) + 1;
            }
        }

        return result;
    }
}
