using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CivilClaudePlugin.Api
{
    public class ClaudeApiClient
    {
        private static readonly HttpClient _http = new HttpClient();
        private readonly string _apiKey;
        private const string Endpoint = "https://api.anthropic.com/v1/messages";

        // Gecici hatalarda (429/5xx/529, ag hatasi) ustel backoff ile en fazla bu kadar dene.
        private const int MaxRetries = 3;
        // Sonsuz tool dongusunu engelle: bir cagri en fazla bu kadar tool turu yapabilir.
        private const int MaxToolIterations = 12;

        private readonly List<Message> _history = new List<Message>();

        public ClaudeApiClient(string apiKey)
        {
            _apiKey = apiKey;
        }

        public void ClearHistory() => _history.Clear();

        // Basit metin cagrisi (tool kullanmadan). Geriye uyumluluk icin korunur.
        public Task<string> SendMessageAsync(string userMessage, string systemPrompt = null)
            => SendMessageAsync(userMessage, systemPrompt, null, null);

        // Tool destekli cagri.
        // tools: istege eklenecek tool tanimlari (null = tool yok).
        // toolExecutor: (toolName, input) -> tool_result icerigi olarak donen metin.
        // Claude bir tool kullanirsa burada calistirilir, sonucu geri yollanir ve
        // stop_reason "tool_use" olmayana kadar dongu devam eder; en sonunda Claude'un
        // metin yaniti dondurulur.
        public async Task<string> SendMessageAsync(string userMessage, string systemPrompt,
            object tools, Func<string, JObject, string> toolExecutor)
        {
            _history.Add(new Message { Role = "user", Content = userMessage });

            for (int iteration = 0; ; iteration++)
            {
                if (iteration >= MaxToolIterations)
                    throw new Exception($"Tool dongusu {MaxToolIterations} turu asti; islem durduruldu.");

                var request = new ClaudeRequest
                {
                    System = systemPrompt,
                    Tools = tools,
                    Messages = BuildRequestMessagesWithCache()
                };

                var response = await PostWithRetryAsync(request);

                // Claude'un tum bloklarini (text + tool_use) gecmise oldugu gibi ekle.
                _history.Add(new Message { Role = "assistant", Content = response.Content });

                // Tool kullanildiysa hepsini calistir, tool_result olarak geri yolla, donguye devam et.
                if (response.StopReason == "tool_use" && toolExecutor != null)
                {
                    var toolResults = new List<ContentBlock>();
                    foreach (var block in response.Content)
                    {
                        if (block.Type != "tool_use") continue;

                        string resultText;
                        bool isError = false;
                        try
                        {
                            resultText = toolExecutor(block.Name, block.Input ?? new JObject());
                        }
                        catch (Exception ex)
                        {
                            resultText = "Hata: " + ex.Message;
                            isError = true;
                        }

                        toolResults.Add(new ContentBlock
                        {
                            Type = "tool_result",
                            ToolUseId = block.Id,
                            ToolResultContent = resultText,
                            IsError = isError ? (bool?)true : null
                        });
                    }

                    _history.Add(new Message { Role = "user", Content = toolResults });
                    continue;
                }

                // Tool yok: metin bloklarini birlestirip don.
                var sb = new StringBuilder();
                foreach (var block in response.Content)
                    if (block.Type == "text" && block.Text != null)
                        sb.Append(block.Text);
                return sb.ToString();
            }
        }

        // Tek bir /messages cagrisini ustel backoff ile yapar. 429/5xx/529 ve ag
        // hatalarinda yeniden dener; kalici hatalarda anlamli mesajla firlatir.
        private async Task<ClaudeResponse> PostWithRetryAsync(ClaudeRequest request)
        {
            var json = JsonConvert.SerializeObject(request, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });

            for (int attempt = 0; ; attempt++)
            {
                HttpResponseMessage httpResponse;
                string body;
                try
                {
                    using (var httpRequest = new HttpRequestMessage(HttpMethod.Post, Endpoint)
                    {
                        Content = new StringContent(json, Encoding.UTF8, "application/json")
                    })
                    {
                        httpRequest.Headers.Add("x-api-key", _apiKey);
                        httpRequest.Headers.Add("anthropic-version", "2023-06-01");
                        httpResponse = await _http.SendAsync(httpRequest);
                    }
                    body = await httpResponse.Content.ReadAsStringAsync();
                }
                catch (HttpRequestException ex)
                {
                    if (attempt < MaxRetries)
                    {
                        await Task.Delay(BackoffMs(attempt, null));
                        continue;
                    }
                    throw new Exception($"Ag hatasi: {ex.Message}");
                }

                int code = (int)httpResponse.StatusCode;

                // Gecici durumlar: rate limit (429), overloaded (529), sunucu hatalari (5xx)
                if ((code == 429 || code == 529 || code >= 500) && attempt < MaxRetries)
                {
                    await Task.Delay(BackoffMs(attempt, httpResponse));
                    continue;
                }

                if (!httpResponse.IsSuccessStatusCode)
                {
                    string msg = TryExtractError(body) ?? body;
                    throw new Exception($"API hatasi (HTTP {code}): {msg}");
                }

                var response = JsonConvert.DeserializeObject<ClaudeResponse>(body);
                if (response == null)
                    throw new Exception("API yaniti cozulemedi.");
                if (response.Error != null)
                    throw new Exception($"API Error: {response.Error.Message}");
                if (response.Content == null || response.Content.Count == 0)
                    throw new Exception("API bos icerik dondurdu.");

                return response;
            }
        }

        // Retry gecikmesi: varsa Retry-After basligina uy, yoksa ustel (0.5s,1s,2s... max 8s).
        private static int BackoffMs(int attempt, HttpResponseMessage httpResponse)
        {
            if (httpResponse != null &&
                httpResponse.Headers.TryGetValues("retry-after", out var vals))
            {
                foreach (var v in vals)
                    if (int.TryParse(v, out var secs))
                        return Math.Min(Math.Max(secs, 1), 30) * 1000;
            }
            int ms = 500 * (1 << attempt); // 500, 1000, 2000, 4000
            return Math.Min(ms, 8000);
        }

        // Hata govdesinden {"error":{"message":...}} cikar; cozulemezse null.
        private static string TryExtractError(string body)
        {
            try { return JsonConvert.DeserializeObject<ClaudeResponse>(body)?.Error?.Message; }
            catch { return null; }
        }

        // Istek mesajlarini hazirlar ve SON mesajin son blocuna cache_control koyar.
        // Render sirasi tools -> system -> messages oldugundan, son mesaja konan breakpoint
        // tum oneki (tool tanimlari + sistem promptu + tum gecmis) cache'ler. Haiku 4.5'te
        // cache_read tokenlari ITPM limitine SAYILMAZ; ardisik cagrilarda onek bedavaya gelir.
        // Saklanan gecmisi bozmamak icin son mesaj klonlanarak isaretlenir.
        private List<Message> BuildRequestMessagesWithCache()
        {
            var msgs = new List<Message>(_history);
            if (msgs.Count == 0) return msgs;

            int last = msgs.Count - 1;
            var lm = msgs[last];

            List<ContentBlock> blocks;
            if (lm.Content is string s)
            {
                blocks = new List<ContentBlock> { new ContentBlock { Type = "text", Text = s } };
            }
            else if (lm.Content is List<ContentBlock> existing)
            {
                blocks = existing.Select(b => b.Clone()).ToList();
            }
            else
            {
                return msgs; // beklenmeyen sekil; cache'siz birak
            }

            if (blocks.Count > 0)
                blocks[blocks.Count - 1].CacheControl = new CacheControl();

            msgs[last] = new Message { Role = lm.Role, Content = blocks };
            return msgs;
        }
    }
}
