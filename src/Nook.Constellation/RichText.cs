using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
namespace Nook.Constellation;
public static class RichText
{
    // A distinct brush identifies the editor's default ink without changing user-selected colors.
    private static readonly SolidColorBrush EditorInk = new(Color.FromRgb(23, 33, 31));
    private static IEnumerable<TextElement> Elements(FlowDocument document)
    {
        for (TextPointer? p = document.ContentStart; p != null && p.CompareTo(document.ContentEnd) < 0; p = p.GetNextContextPosition(LogicalDirection.Forward))
            if (p.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.ElementStart && p.GetAdjacentElement(LogicalDirection.Forward) is TextElement element) yield return element;
    }
    public static void UseEditorColors(RichTextBox box)
    {
        box.Document.Foreground = EditorInk;
        foreach (var element in Elements(box.Document))
            if (element.ReadLocalValue(TextElement.ForegroundProperty) is SolidColorBrush brush && brush.Color == Colors.White)
                element.Foreground = EditorInk;
    }
    public static void ResetEditorColor(TextSelection selection) => selection.ApplyPropertyValue(TextElement.ForegroundProperty, EditorInk);
    public static double DefaultSize { get; set; } = 13;
    public static string Plain(RichTextBox box) => new TextRange(box.Document.ContentStart, box.Document.ContentEnd).Text.TrimEnd('\r','\n');
    public static string Save(RichTextBox box)
    {
        var defaults = new List<TextElement>();
        bool defaultDocument = ReferenceEquals(box.Document.Foreground, EditorInk);
        foreach (var element in Elements(box.Document))
            if (ReferenceEquals(element.ReadLocalValue(TextElement.ForegroundProperty), EditorInk)) defaults.Add(element);
        try
        {
            if (defaultDocument) box.Document.Foreground = Brushes.White;
            foreach (var element in defaults) element.Foreground = Brushes.White;
            using var stream = new MemoryStream(); new TextRange(box.Document.ContentStart, box.Document.ContentEnd).Save(stream, DataFormats.Rtf); return Convert.ToBase64String(stream.ToArray());
        }
        finally { if (defaultDocument) box.Document.Foreground = EditorInk; foreach (var element in defaults) element.Foreground = EditorInk; }
    }
    public static void Load(RichTextBox box, string rtf, string fallback)
    {
        box.Document = new FlowDocument { Foreground = Brushes.White, PagePadding = new Thickness(0), FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = DefaultSize };
        if (!string.IsNullOrEmpty(rtf))
        {
            try { using var stream = new MemoryStream(Convert.FromBase64String(rtf)); new TextRange(box.Document.ContentStart, box.Document.ContentEnd).Load(stream, DataFormats.Rtf); return; }
            catch (Exception e) when (e is FormatException or ArgumentException or IOException) { }
        }
        box.Document.Blocks.Add(new Paragraph(new Run(fallback)) { Margin = new Thickness(0) });
    }
    public static TextBlock Preview(string rtf, string fallback, double size = 0, bool paper = false)
    {
        var result = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = size > 0 ? size : DefaultSize, Foreground = Brushes.White };
        var editor = new RichTextBox(); Load(editor, rtf, fallback);
        if (paper) { UseEditorColors(editor); result.Foreground = Ui.Ink; }
        void Add(InlineCollection source)
        {
            foreach (Inline inline in source)
            {
                if (inline is Run run) result.Inlines.Add(new Run(run.Text) { FontWeight = run.FontWeight, FontStyle = run.FontStyle, FontSize = run.FontSize, FontFamily = run.FontFamily, TextDecorations = run.TextDecorations, Foreground = run.Foreground });
                else if (inline is Span span) Add(span.Inlines);
                else if (inline is LineBreak) result.Inlines.Add(new LineBreak());
            }
        }
        foreach (var block in editor.Document.Blocks)
            if (block is Paragraph p) { if (result.Inlines.Count > 0) result.Inlines.Add(new LineBreak()); Add(p.Inlines); }
        return result;
    }
}
