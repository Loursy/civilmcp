using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CivilClaudePlugin.Api
{
    public class ClaudeRequest
    {
        [JsonProperty("model")]
        public string Model { get; set; } = "claude-haiku-4-5-20251001";

        [JsonProperty("max_tokens")]
        public int MaxTokens { get; set; } = 1024;

        [JsonProperty("system")]
        public string System { get; set; }

        // Tool tanimlari (list of {name, description, input_schema}). Bos birakilirsa gonderilmez.
        [JsonProperty("tools", NullValueHandling = NullValueHandling.Ignore)]
        public object Tools { get; set; }

        // tool_choice belirtilmezse "auto" varsayilir; o yuzden null birakip gondermiyoruz.
        [JsonProperty("tool_choice", NullValueHandling = NullValueHandling.Ignore)]
        public object ToolChoice { get; set; }

        [JsonProperty("messages")]
        public List<Message> Messages { get; set; } = new List<Message>();
    }

    public class Message
    {
        [JsonProperty("role")]
        public string Role { get; set; }

        // string (basit metin) VEYA List<ContentBlock> (tool_use / tool_result / cache_control tasiyan bloklar)
        [JsonProperty("content")]
        public object Content { get; set; }
    }

    // Hem istek hem yanit tarafinda kullanilan evrensel icerik blogu.
    // type'a gore farkli alanlar dolar: text / tool_use / tool_result.
    public class ContentBlock
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
        public string Text { get; set; }

        // tool_use blogu
        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public string Id { get; set; }

        [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
        public string Name { get; set; }

        [JsonProperty("input", NullValueHandling = NullValueHandling.Ignore)]
        public JObject Input { get; set; }

        // tool_result blogu
        [JsonProperty("tool_use_id", NullValueHandling = NullValueHandling.Ignore)]
        public string ToolUseId { get; set; }

        [JsonProperty("content", NullValueHandling = NullValueHandling.Ignore)]
        public string ToolResultContent { get; set; }

        [JsonProperty("is_error", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsError { get; set; }

        // Prompt caching breakpoint (yalnizca son blokta set edilir)
        [JsonProperty("cache_control", NullValueHandling = NullValueHandling.Ignore)]
        public CacheControl CacheControl { get; set; }

        public ContentBlock Clone() => new ContentBlock
        {
            Type = Type,
            Text = Text,
            Id = Id,
            Name = Name,
            Input = Input,
            ToolUseId = ToolUseId,
            ToolResultContent = ToolResultContent,
            IsError = IsError,
            CacheControl = CacheControl
        };
    }

    public class CacheControl
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "ephemeral";
    }

    public class ClaudeResponse
    {
        [JsonProperty("content")]
        public List<ContentBlock> Content { get; set; }

        [JsonProperty("stop_reason")]
        public string StopReason { get; set; }

        [JsonProperty("error")]
        public ApiError Error { get; set; }
    }

    public class ApiError
    {
        [JsonProperty("message")]
        public string Message { get; set; }
    }
}
