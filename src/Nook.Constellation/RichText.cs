using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
namespace Nook.Constellation;
public static class RichText
{
    public static string Plain(RichTextBox box) => new TextRange(box.Document.ContentStart, box.Document.ContentEnd).Text.TrimEnd('\r','\n');
    public static string Save(RichTextBox box)
    {
        using var stream = new MemoryStream(); new TextRange(box.Document.ContentStart, box.Document.ContentEnd).Save(stream, DataFormats.Rtf); return Convert.ToBase64String(stream.ToArray());
    }
    public static void Load(RichTextBox box, string rtf, string fallback)
    {
        box.Document = new FlowDocument { Foreground = Brushes.White, PagePadding = new Thickness(0), FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = 13 };
        if (!string.IsNullOrEmpty(rtf))
        {
            try { using var stream = new MemoryStream(Convert.FromBase64String(rtf)); new TextRange(box.Document.ContentStart, box.Document.ContentEnd).Load(stream, DataFormats.Rtf); return; }
            catch (Exception e) when (e is FormatException or ArgumentException or IOException) { }
        }
        box.Document.Blocks.Add(new Paragraph(new Run(fallback)) { Margin = new Thickness(0) });
    }
    public static TextBlock Preview(string rtf, string fallback, double size = 12)
    {
        var result = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = size, Foreground = Brushes.White };
        var editor = new RichTextBox(); Load(editor, rtf, fallback);
        void Add(InlineCollection source)
        {
            foreach (Inline inline in source)
            {
                if (inline is Run run) result.Inlines.Add(new Run(run.Text) { FontWeight = run.FontWeight, FontStyle = run.FontStyle, Foreground = run.Foreground });
                else if (inline is Span span) Add(span.Inlines);
                else if (inline is LineBreak) result.Inlines.Add(new LineBreak());
            }
        }
        foreach (var block in editor.Document.Blocks)
            if (block is Paragraph p) { if (result.Inlines.Count > 0) result.Inlines.Add(new LineBreak()); Add(p.Inlines); }
        return result;
    }
}
