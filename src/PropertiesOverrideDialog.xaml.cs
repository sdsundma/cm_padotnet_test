using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CMPADotNetTest.Models;

namespace CMPADotNetTest
{
    public partial class PropertiesOverrideDialog : Window
    {
        private readonly Dictionary<string, TextBox> _overrideFields =
            new Dictionary<string, TextBox>();

        public bool IsConfirmed { get; private set; }

        private static readonly string[] FixedKeys = {
            "NAE_Port", "Protocol", "Use_Persistent_Connections",
            "Connection_Timeout", "Connection_Read_Timeout",
            "Connection_Retry_Interval", "Symmetric_Key_Cache_Enabled",
            "Persistent_Cache_Enabled", "Log_File"
        };

        public PropertiesOverrideDialog(
            Window owner,
            Dictionary<string, string> existingOverrides,
            KeyCaching keyCaching,
            string propertiesFilePath)
        {
            InitializeComponent();
            Owner = owner;

            var kcDerived = GetKeyCachingOverrides(keyCaching);
            var fileProps = LoadPropertiesFile(propertiesFilePath);
            var rows = BuildRows(fileProps);

            if (kcDerived.Count > 0)
                TxtLegend.Visibility = Visibility.Visible;

            foreach (var row in rows)
            {
                string key = row[0];
                string fileVal = row[1];

                var rowGrid = MakeRowGrid();

                // Column 0: property name
                var keyBlock = new TextBlock
                {
                    Text = key,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x78, 0x90, 0x9C)),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 0, 4, 0)
                };
                Grid.SetColumn(keyBlock, 0);
                rowGrid.Children.Add(keyBlock);

                // Column 1: file value
                bool notSet = string.IsNullOrEmpty(fileVal);
                var fileBlock = new TextBlock
                {
                    Text = notSet ? "(not set)" : fileVal,
                    Foreground = new SolidColorBrush(notSet
                        ? Color.FromRgb(0x78, 0x90, 0x9C)
                        : Color.FromRgb(0xE0, 0xE0, 0xE0)),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(4, 0, 4, 0)
                };
                Grid.SetColumn(fileBlock, 1);
                rowGrid.Children.Add(fileBlock);

                // Column 2: override value (read-only if key-caching controlled)
                if (kcDerived.ContainsKey(key))
                {
                    var kcBlock = new TextBlock
                    {
                        Text = kcDerived[key],
                        Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xBC, 0xD4)),
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 11,
                        FontStyle = FontStyles.Italic,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(4, 0, 6, 0),
                        ToolTip = "Controlled by Key Caching selection (" + keyCaching + ")"
                    };
                    Grid.SetColumn(kcBlock, 2);
                    rowGrid.Children.Add(kcBlock);
                }
                else
                {
                    string existing = existingOverrides.ContainsKey(key)
                        ? existingOverrides[key] : "";
                    var tb = new TextBox
                    {
                        Text = existing,
                        Background = new SolidColorBrush(Color.FromRgb(0x0D, 0x1B, 0x2A)),
                        Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
                        CaretBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(0x0F, 0x34, 0x60)),
                        BorderThickness = new Thickness(1),
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 11,
                        Padding = new Thickness(4, 1, 4, 1),
                        Height = 22,
                        Margin = new Thickness(4, 2, 6, 2),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(tb, 2);
                    rowGrid.Children.Add(tb);
                    _overrideFields[key] = tb;
                }

                RowsPanel.Children.Add(rowGrid);
            }
        }

        public Dictionary<string, string> GetOverrides()
        {
            var result = new Dictionary<string, string>();
            foreach (var kv in _overrideFields)
            {
                string v = kv.Value.Text.Trim();
                if (!string.IsNullOrEmpty(v))
                    result[kv.Key] = v;
            }
            return result;
        }

        // Returns the property values that the given KeyCaching mode will apply.
        public static Dictionary<string, string> GetKeyCachingOverrides(KeyCaching mode)
        {
            var m = new Dictionary<string, string>();
            switch (mode)
            {
                case KeyCaching.Memory:
                    m["Symmetric_Key_Cache_Enabled"] = "tcp_ok";
                    break;
                case KeyCaching.Disk:
                    m["Symmetric_Key_Cache_Enabled"] = "tcp_ok";
                    m["Persistent_Cache_Enabled"]    = "yes";
                    break;
            }
            return m;
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static Grid MakeRowGrid()
        {
            var g = new Grid { MinHeight = 26 };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            return g;
        }

        private static List<string[]> BuildRows(Dictionary<string, string> fileProps)
        {
            var rows = new List<string[]>();

            // NAE_IP base + any .N tiers
            string baseIp;
            if (fileProps.TryGetValue("NAE_IP", out baseIp))
                rows.Add(new[] { "NAE_IP", baseIp });
            for (int i = 1; i <= 9; i++)
            {
                string v;
                if (fileProps.TryGetValue("NAE_IP." + i, out v))
                    rows.Add(new[] { "NAE_IP." + i, v });
            }

            foreach (string key in FixedKeys)
            {
                string v;
                rows.Add(new[] { key, fileProps.TryGetValue(key, out v) ? v : "" });
            }
            return rows;
        }

        // Parses a Java-style key=value properties file.
        public static Dictionary<string, string> LoadPropertiesFile(string path)
        {
            var props = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return props;
            try
            {
                foreach (var rawLine in File.ReadAllLines(path))
                {
                    var line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line) || line[0] == '#' || line[0] == '!')
                        continue;
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    string k = line.Substring(0, eq).Trim();
                    string v = line.Substring(eq + 1).Trim();
                    props[k] = v;
                }
            }
            catch { /* best effort */ }
            return props;
        }
    }
}
