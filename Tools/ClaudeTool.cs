using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace CivilClaudePlugin.Tools
{
    // Tool sonucunun kullaniciya nasil gosterilecegi.
    public enum ToolResultDisplay
    {
        Confirm, // "✅ {sonuc}" yesil onay mesaji
        Options, // secenek paneli (liste tipi tool'lar)
        Info     // sade koyu mesaj (ham veri dokumu)
    }

    // Tek bir Claude tool'u: tanimi (API'ye gonderilen sema) ve handler'i (calistirildiginda
    // ilgili helper'i cagirip sonuc metnini donduren fonksiyon) bir arada tutar.
    // Boylece her komut icin tanim + dispatch tek yerde yasar - dev switch ve prompt
    // tekrari yerine domain bazli registry'ler yeterli olur.
    public sealed class ClaudeTool
    {
        public string Name { get; set; }
        public string Description { get; set; }

        // JSON-schema (input_schema) olarak serialize edilecek anonim/dictionary nesne.
        public object InputSchema { get; set; }

        public ToolResultDisplay Display { get; set; } = ToolResultDisplay.Confirm;

        // Tool girisini alip tool_result icerigi olarak donen metni uretir.
        public Func<JObject, string> Handler { get; set; }

        // API'nin "tools" alanina gidecek payload'u uretir (deterministik: ayni sira, ayni
        // bytelar -> cache bozulmaz). Bir kez uretip yeniden kullan.
        public static object BuildPayload(IEnumerable<ClaudeTool> tools)
        {
            return tools.Select(t => new
            {
                name = t.Name,
                description = t.Description,
                input_schema = t.InputSchema
            }).ToList();
        }
    }

    // JObject'ten guvenli okuma yardimcilari.
    public static class ToolInput
    {
        public static string Str(JObject input, string key, string def = "")
        {
            var t = input?[key];
            if (t == null || t.Type == JTokenType.Null) return def;
            return t.ToString();
        }

        public static bool Bool(JObject input, string key, bool def = false)
        {
            var t = input?[key];
            if (t == null || t.Type == JTokenType.Null) return def;
            if (t.Type == JTokenType.Boolean) return (bool)t;
            return string.Equals(t.ToString(), "true", StringComparison.OrdinalIgnoreCase);
        }

        public static double Num(JObject input, string key, double def = 0)
        {
            var t = input?[key];
            if (t == null || t.Type == JTokenType.Null) return def;
            if (t.Type == JTokenType.Integer || t.Type == JTokenType.Float) return (double)t;
            if (double.TryParse(t.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var r)) return r;
            if (double.TryParse(t.ToString().Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var r2)) return r2;
            return def;
        }

        public static int Int(JObject input, string key, int def = 0)
        {
            var t = input?[key];
            if (t == null || t.Type == JTokenType.Null) return def;
            if (t.Type == JTokenType.Integer) return (int)t;
            if (int.TryParse(t.ToString(), out var r)) return r;
            return def;
        }
    }
}
