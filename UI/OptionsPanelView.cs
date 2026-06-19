using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CivilClaudePlugin.UI
{
    // Ham liste metnini ("Kategori:\n  - deger") kategori basliklari altinda gruplanmis
    // tiklanabilir butonlara cevirip gosterir. Bir butona tiklaninca secilen
    // "kategori: deger" (kategori yoksa sadece "deger") metni onPick callback'i ile geri verilir.
    public class OptionsPanelView
    {
        private readonly WrapPanel _panel;
        private readonly Action<string> _onPick;

        // Panele yerlestirilecek dis kapsayici (ClaudePanel grid'e ekler).
        public ScrollViewer View { get; }

        public OptionsPanelView(Action<string> onPick)
        {
            _onPick = onPick;
            _panel = new WrapPanel { Orientation = Orientation.Vertical };
            View = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                MaxHeight = 150,
                Margin = new Thickness(8, 0, 8, 0),
                Content = _panel
            };
        }

        public void Clear() => _panel.Children.Clear();

        public void Show(string rawList)
        {
            Clear();
            if (string.IsNullOrWhiteSpace(rawList)) return;

            var lines = rawList.Replace("\r", "").Split('\n');
            string currentCategory = "";
            var categoryRows = new Dictionary<string, WrapPanel>();
            var seen = new HashSet<string>();

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0) continue;

                if (trimmed.StartsWith("- "))
                {
                    string item = trimmed.Substring(2).Trim();
                    if (item.Length > 0)
                        AddToCategory(categoryRows, seen, currentCategory, item);
                }
                else if (trimmed.Contains(":"))
                {
                    int idx = trimmed.IndexOf(':');
                    string left = trimmed.Substring(0, idx).Trim();
                    string right = (idx + 1 < trimmed.Length) ? trimmed.Substring(idx + 1).Trim() : "";
                    currentCategory = left;
                    EnsureCategoryRow(categoryRows, currentCategory);
                    if (right.Length > 0)
                        AddToCategory(categoryRows, seen, left, right);
                }
            }
        }

        private WrapPanel EnsureCategoryRow(Dictionary<string, WrapPanel> rows, string category)
        {
            if (string.IsNullOrEmpty(category)) category = "Seçenekler";
            if (rows.ContainsKey(category)) return rows[category];

            var header = new TextBlock
            {
                Text = category,
                Foreground = new SolidColorBrush(Color.FromRgb(120, 170, 220)),
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                Margin = new Thickness(2, 6, 0, 2)
            };
            _panel.Children.Add(header);

            var row = new WrapPanel { Orientation = Orientation.Horizontal };
            _panel.Children.Add(row);
            rows[category] = row;
            return row;
        }

        private void AddToCategory(Dictionary<string, WrapPanel> rows, HashSet<string> seen, string category, string value)
        {
            string key = (category ?? "") + "||" + value;
            if (seen.Contains(key)) return;
            seen.Add(key);

            var row = EnsureCategoryRow(rows, category);
            row.Children.Add(MakeOptionButton(category, value));
        }

        private Button MakeOptionButton(string category, string value)
        {
            var btn = new Button
            {
                Content = value,
                Margin = new Thickness(3),
                Padding = new Thickness(8, 3, 8, 3),
                Background = new SolidColorBrush(Color.FromRgb(55, 65, 80)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                ToolTip = string.IsNullOrEmpty(category) ? value : $"{category}: {value}"
            };
            string cat = category;
            string val = value;
            btn.Click += (s, e) => _onPick(string.IsNullOrEmpty(cat) ? val : $"{cat}: {val}");
            return btn;
        }
    }
}
