using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace KaktusCode;

public class KaktuscodeInterpreter
{
    private class WinCtx
    {
        public Window Window { get; }
        public StackPanel Panel { get; }

        public WinCtx(string title)
        {
            Panel = new StackPanel { Spacing = 8, Margin = new Thickness(12) };
            Window = new Window
            {
                Title = string.IsNullOrWhiteSpace(title) ? "Window" : title,
                Width = 600,
                Height = 400,
                Content = new ScrollViewer { Content = Panel }
            };
        }
    }

    private readonly Dictionary<string, string> _vars = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, WinCtx> _wins = new(StringComparer.OrdinalIgnoreCase);
    private readonly Window _main;
    
    public KaktuscodeInterpreter(Window mainWindow)
    {
        _main = mainWindow;
    }

    public async Task<string> Run(string code)
    {
        var outBuf = new StringBuilder();
        var lines = code.Replace("\r\n", "\n").Split('\n');

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;

            line = Expand(line);

            var parts = Tokenize(line);
            if (parts.Count == 0) continue;

            var cmd = parts[0].ToLowerInvariant();

            try
            {
                switch (cmd)
                {
                    case "say":
                        outBuf.AppendLine(parts.Count > 1 ? string.Join(" ", parts.GetRange(1, parts.Count-1)) : "");
                        break;

                    case "wait":
                        if (parts.Count < 2 || !int.TryParse(parts[1], out var secs))
                            outBuf.AppendLine("❌ usage: wait <seconds>");
                        else {
                            await Task.Delay(Math.Max(0, secs) * 1000);
                            outBuf.AppendLine($"(waited {secs} sec)");
                        }
                        break;

                    case "set":
                        if (parts.Count < 3) { outBuf.AppendLine("❌ usage: set <name> <value>"); break; }
                        _vars[parts[1]] = string.Join(" ", parts.GetRange(2, parts.Count-2));
                        outBuf.AppendLine($"✔ set {parts[1]}");
                        break;

                    case "title":
                        {
                            var title = parts.Count > 1 ? string.Join(" ", parts.GetRange(1, parts.Count-1)) : "";
                            await UI(() => _main.Title = title);
                            outBuf.AppendLine($"🪟 title = {title}");
                        }
                        break;

                    case "color":
                        if (parts.Count < 2) { outBuf.AppendLine("❌ usage: color <name|#hex>"); break; }
                        await UI(() => {
                            try { _main.Background = new SolidColorBrush(Color.Parse(parts[1])); }
                            catch { outBuf.AppendLine("❌ invalid color"); }
                        });
                        break;

                    case "openurl":
                        if (parts.Count < 2) { outBuf.AppendLine("❌ usage: openurl <url>"); break; }
                        OpenSystemBrowser(parts[1]);
                        outBuf.AppendLine($"🌐 opening {parts[1]}");
                        break;

                    case "window":
                        if (parts.Count < 2) { outBuf.AppendLine("❌ usage: window <id> [\"title\"]"); break; }
                        {
                            var id = parts[1];
                            var title = parts.Count >= 3 ? string.Join(" ", parts.GetRange(2, parts.Count-2)) : id;
                            await UI(() => {
                                var ctx = new WinCtx(title);
                                _wins[id] = ctx;
                            });
                            outBuf.AppendLine($"🪟 window '{id}' created");
                        }
                        break;

                    case "show":
                        if (parts.Count < 2) { outBuf.AppendLine("❌ usage: show <id>"); break; }
                        await UI(() => {
                            if (_wins.TryGetValue(parts[1], out var w)) w.Window.Show();
                            else outBuf.AppendLine($"❌ no such window '{parts[1]}'");
                        });
                        break;

                    case "close":
                        if (parts.Count < 2) { outBuf.AppendLine("❌ usage: close <id>"); break; }
                        await UI(() => {
                            if (_wins.TryGetValue(parts[1], out var w)) w.Window.Close();
                            else outBuf.AppendLine($"❌ no such window '{parts[1]}'");
                        });
                        break;

                    case "setbg":
                        if (parts.Count < 3) { outBuf.AppendLine("❌ usage: setbg <id> <color>"); break; }
                        await UI(() => {
                            if (_wins.TryGetValue(parts[1], out var w))
                            {
                                try { w.Window.Background = new SolidColorBrush(Color.Parse(parts[2])); }
                                catch { outBuf.AppendLine("❌ invalid color"); }
                            }
                            else outBuf.AppendLine($"❌ no such window '{parts[1]}'");
                        });
                        break;

                    case "label":
                        if (parts.Count < 3) { outBuf.AppendLine("❌ usage: label <id> \"text\""); break; }
                        await UI(() => {
                            if (_wins.TryGetValue(parts[1], out var w))
                                w.Panel.Children.Add(new TextBlock { Text = string.Join(" ", parts.GetRange(2, parts.Count-2)), FontSize = 16 });
                            else outBuf.AppendLine($"❌ no such window '{parts[1]}'");
                        });
                        break;

                    case "button":
                        if (parts.Count < 3) { outBuf.AppendLine("❌ usage: button <id> \"text\""); break; }
                        await UI(() => {
                            if (_wins.TryGetValue(parts[1], out var w))
                            {
                                var b = new Button { Content = string.Join(" ", parts.GetRange(2, parts.Count-2)), Height = 36 };
                                b.Click += (_, __) =>
                                {
                                    if (b.Content is string s) b.Content = s.EndsWith("✓") ? s.TrimEnd(' ', '✓') : s + " ✓";
                                };
                                w.Panel.Children.Add(b);
                            }
                            else outBuf.AppendLine($"❌ no such window '{parts[1]}'");
                        });
                        break;

                    case "clear":
                        if (parts.Count < 2) { outBuf.AppendLine("❌ usage: clear <id>"); break; }
                        await UI(() => {
                            if (_wins.TryGetValue(parts[1], out var w)) w.Panel.Children.Clear();
                            else outBuf.AppendLine($"❌ no such window '{parts[1]}'");
                        });
                        break;

                    default:
                        outBuf.AppendLine("❓ unknown command: " + cmd);
                        break;
                }
            }
            catch (Exception ex)
            {
                outBuf.AppendLine("❌ " + ex.Message);
            }
        }

        return outBuf.ToString();
    }

    // --- helpers ---

    private string Expand(string line)
    {
        foreach (var (k, v) in _vars)
            line = line.Replace($"${k}", v);
        return line;
    }

    private static List<string> Tokenize(string line)
    {
        var list = new List<string>();
        var sb = new StringBuilder();
        bool inQuote = false;

        foreach (var ch in line)
        {
            if (ch == '"') { inQuote = !inQuote; continue; }
            if (!inQuote && char.IsWhiteSpace(ch))
            {
                if (sb.Length > 0) { list.Add(sb.ToString()); sb.Clear(); }
            }
            else sb.Append(ch);
        }
        if (sb.Length > 0) list.Add(sb.ToString());
        return list;
    }

    private static Task UI(Action action) =>
        Dispatcher.UIThread.InvokeAsync(action).GetTask();

    private static void OpenSystemBrowser(string url)
    {
        try
        {
            using var p = new Process();
            p.StartInfo.UseShellExecute = true;
            p.StartInfo.FileName = url;
            p.Start();
        }
        catch { /* ignore */ }
    }
}