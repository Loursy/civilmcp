using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CivilClaudePlugin.Api;
using CivilClaudePlugin.Helpers;
using CivilClaudePlugin.Tools;
using Newtonsoft.Json.Linq;

namespace CivilClaudePlugin.UI
{
    public class ChatMessage
    {
        public string Text { get; set; }
        public Brush Background { get; set; }
    }

    public class ClaudePanel : UserControl
    {
        private ClaudeApiClient _client;
        private readonly ObservableCollection<ChatMessage> _messages = new ObservableCollection<ChatMessage>();
        private bool _injectContextNext = false;
        private MacroRunner _macro;

        // Tool registry: tum cizim komutlari JSON protokolu yerine tool_use uzerinden.
        private readonly System.Collections.Generic.Dictionary<string, ClaudeTool> _tools;
        private readonly object _toolsPayload;
        // Bir tur icinde calistirilan tool isimleri (makro tamamlamasi icin).
        private readonly System.Collections.Generic.List<string> _executedTools = new System.Collections.Generic.List<string>();

        private TextBox _userInput;
        private PasswordBox _apiKeyBox;
        private ListBox _chatMessages;
        private ScrollViewer _chatScroll;
        private OptionsPanelView _options;

        public ClaudePanel()
        {
            _tools = AssistantConfig.BuildTools();
            _toolsPayload = ClaudeTool.BuildPayload(_tools.Values);

            BuildUI();
            this.Loaded += (s, e) => LoadApiKey();
        }

        // Claude bir tool kullandiginda ClaudeApiClient buradan calistirir (UI thread'inde).
        // Sonuc metni tool_result olarak Claude'a geri doner; ayrica kullaniciya gosterilir.
        private string ExecuteTool(string name, JObject input)
        {
            _executedTools.Add(name);

            if (!_tools.TryGetValue(name, out var tool))
                return $"Bilinmeyen arac: {name}";

            var result = tool.Handler(input ?? new JObject());
            switch (tool.Display)
            {
                case ToolResultDisplay.Options:
                    _options.Show(result);
                    break;
                case ToolResultDisplay.Info:
                    AddMessage(result, new SolidColorBrush(Color.FromRgb(40, 40, 40)));
                    break;
                default:
                    AddMessage($"✅ {result}", new SolidColorBrush(Color.FromRgb(30, 80, 30)));
                    break;
            }
            return result;
        }

        private void BuildUI()
        {
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 8, 8, 4) };
            var title = new TextBlock { Text = "Claude AI", Foreground = Brushes.White, FontSize = 16, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
            var clearBtn = new Button { Content = "Temizle", Margin = new Thickness(12, 0, 0, 0), Padding = new Thickness(8, 2, 8, 2), Background = new SolidColorBrush(Color.FromRgb(68, 68, 68)), Foreground = Brushes.White, BorderThickness = new Thickness(0) };
            var contextBtn = new Button { Content = "📋 Bağlam Ekle", Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(8, 2, 8, 2), Background = new SolidColorBrush(Color.FromRgb(45, 106, 159)), Foreground = Brushes.White, BorderThickness = new Thickness(0) };
            var macroBtn = new Button { Content = "🔧 Catchbasin", Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(8, 2, 8, 2), Background = new SolidColorBrush(Color.FromRgb(120, 80, 40)), Foreground = Brushes.White, BorderThickness = new Thickness(0) };
            clearBtn.Click += ClearChat_Click;
            contextBtn.Click += InjectContext_Click;
            macroBtn.Click += async (s, e) => await StartCatchbasinMacro();
            header.Children.Add(title);
            header.Children.Add(clearBtn);
            header.Children.Add(contextBtn);
            header.Children.Add(macroBtn);
            Grid.SetRow(header, 0);

            var apiKeyPanel = new StackPanel { Margin = new Thickness(8, 4, 8, 4) };
            apiKeyPanel.Children.Add(new TextBlock { Text = "API Key:", Foreground = Brushes.Gray, FontSize = 11 });
            _apiKeyBox = new PasswordBox { Background = new SolidColorBrush(Color.FromRgb(51, 51, 51)), Foreground = Brushes.White, BorderThickness = new Thickness(0), Padding = new Thickness(6) };
            _apiKeyBox.PasswordChanged += (s, e) => SaveApiKey();
            apiKeyPanel.Children.Add(_apiKeyBox);
            Grid.SetRow(apiKeyPanel, 1);

            _chatScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(8, 4, 8, 4) };
            _chatMessages = new ListBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0), ItemsSource = _messages };
            _chatMessages.ItemTemplate = CreateMessageTemplate();
            _chatScroll.Content = _chatMessages;
            Grid.SetRow(_chatScroll, 2);

            // Secenekler paneli (tiklanabilir butonlar) - secim input'a eklenir
            _options = new OptionsPanelView(AppendToInput);
            Grid.SetRow(_options.View, 3);

            var inputGrid = new Grid { Margin = new Thickness(8, 4, 8, 8) };
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _userInput = new TextBox { Background = new SolidColorBrush(Color.FromRgb(51, 51, 51)), Foreground = Brushes.White, BorderThickness = new Thickness(0), Padding = new Thickness(8), FontSize = 13, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, MaxHeight = 100 };
            _userInput.KeyDown += UserInput_KeyDown;
            var sendBtn = new Button { Content = "Gönder", Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(12, 8, 12, 8), Background = new SolidColorBrush(Color.FromRgb(45, 106, 159)), Foreground = Brushes.White, BorderThickness = new Thickness(0) };
            sendBtn.Click += Send_Click;
            Grid.SetColumn(_userInput, 0);
            Grid.SetColumn(sendBtn, 1);
            inputGrid.Children.Add(_userInput);
            inputGrid.Children.Add(sendBtn);
            Grid.SetRow(inputGrid, 4);

            grid.Children.Add(header);
            grid.Children.Add(apiKeyPanel);
            grid.Children.Add(_chatScroll);
            grid.Children.Add(_options.View);
            grid.Children.Add(inputGrid);

            Content = grid;
        }

        private DataTemplate CreateMessageTemplate()
        {
            var template = new DataTemplate();
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.MarginProperty, new Thickness(0, 4, 0, 4));
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(8));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            borderFactory.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background"));
            var textFactory = new FrameworkElementFactory(typeof(TextBlock));
            textFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Text"));
            textFactory.SetValue(TextBlock.ForegroundProperty, Brushes.White);
            textFactory.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
            textFactory.SetValue(TextBlock.FontSizeProperty, 13.0);
            borderFactory.AppendChild(textFactory);
            template.VisualTree = borderFactory;
            return template;
        }

        private ClaudeApiClient GetClient()
        {
            var key = _apiKeyBox.Password?.Trim();
            if (string.IsNullOrEmpty(key))
            {
                AddMessage("⚠ Lütfen API key girin.", Brushes.DarkRed);
                return null;
            }
            if (_client == null)
                _client = new ClaudeApiClient(key);
            return _client;
        }

        private async void Send_Click(object sender, RoutedEventArgs e) => await SendMessage();

        private async void UserInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.IsKeyDown(Key.LeftShift))
            {
                e.Handled = true;
                await SendMessage();
            }
        }

        private async Task SendMessage()
        {
            var text = _userInput.Text?.Trim();
            if (string.IsNullOrEmpty(text)) return;

            var client = GetClient();
            if (client == null) return;

            _options.Clear();

            if (_injectContextNext)
            {
                var ctx = CivilContextHelper.GetDrawingContext();
                text = $"[Drawing Context]\n{ctx}\n\n[User Question]\n{text}";
                _injectContextNext = false;
            }

            AddMessage($"Siz: {_userInput.Text.Trim()}", new SolidColorBrush(Color.FromRgb(45, 70, 95)));
            _userInput.Clear();

            AddMessage("Claude düşünüyor...", new SolidColorBrush(Color.FromRgb(50, 50, 50)));

            try
            {
                _executedTools.Clear();
                var reply = await client.SendMessageAsync(text, AssistantConfig.SystemPrompt, _toolsPayload, ExecuteTool);
                _messages.RemoveAt(_messages.Count - 1);

                // Tool sonuclari zaten ExecuteTool icinde gosterildi; burada yalnizca
                // Claude'un (varsa) metin yanitini goster.
                if (!string.IsNullOrWhiteSpace(reply))
                    AddMessage($"Claude: {reply}", new SolidColorBrush(Color.FromRgb(40, 40, 40)));

                // Makro aktifse, bu turda calistirilan tool'lardan beklenen tamamlama tool'u
                // varsa zincire devam et (snapshot al; bildirim re-entrant SendMessage tetikleyebilir).
                if (_macro != null && _macro.IsActive)
                {
                    foreach (var executed in _executedTools.ToList())
                        await _macro.NotifyCommandExecutedAsync(executed);
                }
            }
            catch (System.Exception ex)
            {
                if (_messages.Count > 0 && _messages[_messages.Count - 1].Text == "Claude düşünüyor...")
                    _messages.RemoveAt(_messages.Count - 1);
                AddMessage($"Hata: {ex.Message}", Brushes.DarkRed);
            }

            _chatScroll.ScrollToBottom();
        }

        // ---- MAKRO MOTORU ----
        // "Catchbasin Modelle" makrosunu baslat
        private async Task StartCatchbasinMacro()
        {
            if (_macro != null && _macro.IsActive)
            {
                AddMessage("⚠ Zaten çalışan bir makro var. Önce 'Temizle' ile iptal edin.", Brushes.DarkRed);
                return;
            }

            var client = GetClient();
            if (client == null) return;

            _macro = MacroRunner.CreateCatchbasinMacro(
                log: m => AddMessage(m, new SolidColorBrush(Color.FromRgb(60, 50, 80))),
                sendToClaude: SendMacroMessage);

            await _macro.StartAsync();
        }

        // Makronun etkilesimli adimini, kullanici yazmis gibi normal akistan Claude'a gonder
        private async Task SendMacroMessage(string text)
        {
            _userInput.Text = text;
            await SendMessage();
        }

        // OptionsPanelView'dan secilen secenegi giris kutusuna ekle.
        private void AppendToInput(string addition)
        {
            if (!string.IsNullOrEmpty(_userInput.Text) && !_userInput.Text.EndsWith("\n"))
                _userInput.Text += "\n";
            _userInput.Text += addition;
            _userInput.CaretIndex = _userInput.Text.Length;
            _userInput.Focus();
        }

        private void AddMessage(string text, Brush bg)
        {
            _messages.Add(new ChatMessage { Text = text, Background = bg });
        }

        private void ClearChat_Click(object sender, RoutedEventArgs e)
        {
            _macro?.Cancel();
            _macro = null;
            _messages.Clear();
            _client?.ClearHistory();
            _options.Clear();
        }

        private void LoadApiKey()
        {
            var key = ApiKeyStore.Load();
            if (!string.IsNullOrEmpty(key))
                _apiKeyBox.Password = key;
        }

        private void SaveApiKey() => ApiKeyStore.Save(_apiKeyBox.Password);

        private void InjectContext_Click(object sender, RoutedEventArgs e)
        {
            _injectContextNext = true;
            AddMessage("ℹ Drawing context bir sonraki mesaja eklenecek.", new SolidColorBrush(Color.FromRgb(47, 79, 79)));
        }
    }
}