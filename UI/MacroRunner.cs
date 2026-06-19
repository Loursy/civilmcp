using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CivilClaudePlugin.Helpers;

namespace CivilClaudePlugin.UI
{
    // Tek bir makro adimi: ya OTOMATIK (Action calisir) ya da ETKILESIMLI (Claude'a devredilir).
    public class MacroStep
    {
        public string Description;        // kullaniciya gosterilen aciklama
        public bool Interactive;          // true ise Claude'a devredilir
        public Func<string> Action;       // otomatik adim: helper'i cagirir, sonuc metni doner
        public string TriggerMessage;     // etkilesimli: Claude'a "kullanici yazmis gibi" gonderilecek istek
        public string CompletionCommand;  // etkilesimli: bu komut calisinca adim tamamlanir
    }

    // Cok adimli is akisi motoru.
    // Otomatik adimlari arka arkaya calistirir; etkilesimli adimlarda Claude'a devreder
    // ve ilgili tamamlama komutu calistiginda zincire kaldigi yerden devam eder.
    public class MacroRunner
    {
        private readonly List<MacroStep> _steps;
        private readonly Action<string> _log;             // sohbete makro durum mesaji ekler
        private readonly Func<string, Task> _sendToClaude; // metni normal akistan Claude'a gonderir
        private int _current = -1;
        private string _waitingFor;                        // beklenen tamamlama komutu

        public bool IsActive { get; private set; }
        public string Name { get; }

        public MacroRunner(string name, List<MacroStep> steps, Action<string> log, Func<string, Task> sendToClaude)
        {
            Name = name;
            _steps = steps;
            _log = log;
            _sendToClaude = sendToClaude;
        }

        public async Task StartAsync()
        {
            IsActive = true;
            _current = 0;
            _log($"🔧 '{Name}' makrosu basladi ({_steps.Count} adim).");
            await AdvanceAsync();
        }

        public void Cancel()
        {
            if (IsActive) _log($"⏹ '{Name}' makrosu iptal edildi.");
            IsActive = false;
            _waitingFor = null;
            _current = -1;
        }

        // Otomatik adimlari calistir; etkilesimli adimda Claude'a devredip bekle.
        private async Task AdvanceAsync()
        {
            while (IsActive && _current < _steps.Count)
            {
                var step = _steps[_current];
                _log($"▶ Adim {_current + 1}/{_steps.Count}: {step.Description}");

                if (!step.Interactive)
                {
                    string result;
                    try { result = step.Action != null ? step.Action() : "(islem yok)"; }
                    catch (Exception ex) { result = "Hata: " + ex.Message; }
                    _log("   " + result);
                    _current++;
                }
                else
                {
                    _waitingFor = step.CompletionCommand;
                    _log("   (Claude size secenekleri soracak; lutfen secim yapip gonderin.)");
                    await _sendToClaude(step.TriggerMessage);
                    return; // komut bitene kadar bekle (NotifyCommandExecutedAsync devam ettirir)
                }
            }

            if (IsActive && _current >= _steps.Count)
            {
                IsActive = false;
                _waitingFor = null;
                _log($"✅ '{Name}' makrosu tamamlandi.");
            }
        }

        // Panel her komut calistirinca cagirir. Beklenen tamamlama komutuysa zincire devam et.
        public async Task NotifyCommandExecutedAsync(string command)
        {
            if (!IsActive || string.IsNullOrEmpty(_waitingFor)) return;
            if (!string.Equals(command, _waitingFor, StringComparison.OrdinalIgnoreCase)) return;

            _waitingFor = null;
            _current++;
            await AdvanceAsync();
        }

        // ---------------------------------------------------------------------
        // "Catchbasin Modelle" makrosu (Yol 1: tam otomatik zincir).
        // ---------------------------------------------------------------------
        public static MacroRunner CreateCatchbasinMacro(Action<string> log, Func<string, Task> sendToClaude)
        {
            var steps = new List<MacroStep>
            {
                // 1) Kirmizi polyline'lardan alignment olustur (etkilesimli)
                new MacroStep
                {
                    Description = "Kirmizi polyline'lardan alignment olustur",
                    Interactive = true,
                    TriggerMessage = "Kırmızı renkli polylinelardan alignment oluştur. Hangi stil, label set, katman, ana güzergah ve isimlendirmenin hangi numaradan başlayacağını (startNumber) bana sor.",
                    CompletionCommand = "create_alignments"
                },

                // 2) Alignment isimlerine "CB " oneki ekle (otomatik)
                new MacroStep
                {
                    Description = "Alignment isimlerine 'CB ' oneki ekle",
                    Action = () => AlignmentRenamer.AddPrefixSuffix("CB ", "")
                },

                // 3) Alignment'lardan pipe network olustur (etkilesimli)
                new MacroStep
                {
                    Description = "Alignment'lardan pipe network olustur",
                    Interactive = true,
                    TriggerMessage = "Alignmentlardan pipe network oluştur. Ayarları tek tek, her seferinde tek soru sorarak topla (parts list, pipe ailesi, pipe size, structure ailesi, structure size, pipe stili, structure stili, yüzey).",
                    CompletionCommand = "create_pipe_network"
                },

                // 4) CB MAIN pipe network'unu sil (otomatik) - ana guzergah ag istenmiyor
                new MacroStep
                {
                    Description = "CB MAIN pipe network'unu sil",
                    Action = () => PipeNetworkDeleter.DeleteNetworks("CB MAIN")
                },

                // 5) CB MAIN alignment'ini sil (otomatik) - ana guzergah istenmiyor
                new MacroStep
                {
                    Description = "CB MAIN alignment'ini sil",
                    Action = () => AlignmentDeleter.DeleteByFilter("CB MAIN", "", "", "", "", double.NaN, double.NaN, false)
                },

                // 6) Ayni noktadaki duplicate structure'lari sil (otomatik)
                new MacroStep
                {
                    Description = "Ayni noktadaki duplicate structure'lari sil",
                    Action = () => PipeNetworkDeleter.DeleteDuplicateStructures(0.5)
                },

                // 7) Tum structure'lara apply rules (otomatik) - once structure'lar guncel kota gelsin
                new MacroStep
                {
                    Description = "Tum structure'lara apply rules",
                    Action = () => RuleApplier.ApplyRules("structure")
                },

                // 8) Tum borulara apply rules (otomatik) - structure'lar guncelken pipe'lar uygulanir
                new MacroStep
                {
                    Description = "Tum borulara apply rules",
                    Action = () => RuleApplier.ApplyRules("pipe")
                },

                // 9) Parcalari network adiyla adlandir (.1 .2) (otomatik)
                new MacroStep
                {
                    Description = "Parcalari network adiyla adlandir (.1 .2)",
                    Action = () => PartRenamer.NameByNetwork("all")
                },

                // 10) Boru isimlerinden "CB " onekini kaldir (otomatik)
                new MacroStep
                {
                    Description = "Boru isimlerinden 'CB ' onekini kaldir",
                    Action = () => PartRenamer.FindReplace("pipe", "", "CB ", "", "name")
                },

                // 11) Structure aciklamalarina isimlerini kopyala (otomatik)
                new MacroStep
                {
                    Description = "Structure aciklamalarina isimlerini kopyala",
                    Action = () => PartProperties.CopyField("structure", "", "name", "description")
                },

                // 12) Structure aciklamalarindan "CB " kaldir (otomatik)
                new MacroStep
                {
                    Description = "Structure aciklamalarindan 'CB ' kaldir",
                    Action = () => PartRenamer.FindReplace("structure", "", "CB ", "", "description")
                },

                // 13) Nesne stili "CB Gully" olan structure'lara etiket (etkilesimli)
                new MacroStep
                {
                    Description = "Nesne stili 'CB Gully' olan structure'lara etiket",
                    Interactive = true,
                    TriggerMessage = "Nesne stili 'CB Gully' olan structurelara etiket ekle. Hangi structure label stilini kullanacağımı bana sor; boru etiketi ekleme.",
                    CompletionCommand = "add_labels"
                },

                // 14) Nesne stili "CB Pipe" olan borulara etiket (etkilesimli)
                new MacroStep
                {
                    Description = "Nesne stili 'CB Pipe' olan borulara etiket",
                    Interactive = true,
                    TriggerMessage = "Nesne stili 'CB Pipe' olan borulara etiket ekle. Hangi pipe label stilini kullanacağımı bana sor; structure etiketi ekleme.",
                    CompletionCommand = "add_labels"
                },
            };

            return new MacroRunner("Catchbasin Modelle", steps, log, sendToClaude);
        }
    }
}
