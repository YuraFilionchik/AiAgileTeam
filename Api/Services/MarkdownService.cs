using Markdig;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace AiAgileTeam.Services;

public class MarkdownService
{
    private readonly MarkdownPipeline _pipeline;

    public MarkdownService()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
    }

    public void RenderMarkdown(IContainer container, string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            container.Text("");
            return;
        }

        var document = Markdown.Parse(markdown, _pipeline);

        container.Column(col =>
        {
            col.Spacing(4);

            foreach (var block in document)
            {
                col.Item().Element(c => RenderBlock(c, block));
            }
        });
    }

    private void RenderBlock(IContainer container, Block block)
    {
        switch (block)
        {
            case ParagraphBlock paragraph:
                RenderParagraph(container, paragraph);
                break;

            case HeadingBlock heading:
                RenderHeading(container, heading);
                break;

            case ListBlock list:
                RenderList(container, list);
                break;

            case CodeBlock codeBlock:
                RenderCodeBlock(container, codeBlock);
                break;

            case QuoteBlock quote:
                RenderQuote(container, quote);
                break;

            case ThematicBreakBlock:
                container.PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
                break;

            default:
                var text = block.ToString() ?? "";
                container.Text(text);
                break;
        }
    }

    private void RenderParagraph(IContainer container, ParagraphBlock paragraph)
    {
        container.Text(text => RenderInline(text, paragraph.Inline));
    }

    private void RenderHeading(IContainer container, HeadingBlock heading)
    {
        var fontSize = heading.Level switch
        {
            1 => 24,
            2 => 20,
            3 => 16,
            4 => 14,
            _ => 12
        };

        container.Text(text => RenderInline(text, heading.Inline, fontSize, Colors.Blue.Medium, isBold: true));
    }

    private void RenderList(IContainer container, ListBlock list)
    {
        var isOrdered = list.IsOrdered;
        var index = 0;

        container.Column(col =>
        {
            col.Spacing(2);

            foreach (var item in list)
            {
                if (item is ListItemBlock listItem)
                {
                    index++;
                    var prefix = isOrdered ? $"{index}." : "•";
                    
                    col.Item().Row(row =>
                    {
                        row.ConstantItem(20).Text(prefix);
                        row.RelativeItem().Column(itemCol =>
                        {
                            foreach (var child in listItem)
                            {
                                if (child is ParagraphBlock p)
                                {
                                    itemCol.Item().Element(c => RenderParagraph(c, p));
                                }
                                else if (child is ListBlock nestedList)
                                {
                                    itemCol.Item().Element(c => RenderList(c, nestedList));
                                }
                            }
                        });
                    });
                }
            }
        });
    }

    private static void RenderInline(
        TextDescriptor text,
        Inline? inline,
        float? fontSize = null,
        string? fontColor = null,
        bool isBold = false,
        bool isItalic = false,
        bool isMonospace = false,
        bool isUnderline = false)
    {
        var current = inline;
        while (current != null)
        {
            switch (current)
            {
                case LiteralInline literal:
                    var spanText = literal.Content.ToString();
                    if (!string.IsNullOrEmpty(spanText))
                    {
                        ApplyStyle(text.Span(spanText), fontSize, fontColor, isBold, isItalic, isMonospace, isUnderline);
                    }
                    break;

                case EmphasisInline emphasis:
                    var emphasisBold = isBold || emphasis.DelimiterCount >= 2;
                    var emphasisItalic = isItalic || emphasis.DelimiterCount == 1;
                    RenderInline(text, emphasis.FirstChild, fontSize, fontColor, emphasisBold, emphasisItalic, isMonospace, isUnderline);
                    break;

                case CodeInline code:
                    ApplyStyle(text.Span(code.Content), fontSize, fontColor, isBold, isItalic, isMonospace: true, isUnderline);
                    break;

                case LinkInline link:
                    RenderInline(text, link.FirstChild, fontSize, Colors.Blue.Medium, isBold, isItalic, isMonospace, isUnderline: true);
                    if (!string.IsNullOrWhiteSpace(link.Url))
                    {
                        ApplyStyle(text.Span($" ({link.Url})"), fontSize, Colors.Blue.Medium, false, false, false, false);
                    }
                    break;

                case LineBreakInline:
                    text.Span("\n");
                    break;

                case ContainerInline containerInline:
                    RenderInline(text, containerInline.FirstChild, fontSize, fontColor, isBold, isItalic, isMonospace, isUnderline);
                    break;
            }

            current = current.NextSibling;
        }
    }

    private static void ApplyStyle(
        TextSpanDescriptor span,
        float? fontSize,
        string? fontColor,
        bool isBold,
        bool isItalic,
        bool isMonospace,
        bool isUnderline)
    {
        if (fontSize.HasValue)
        {
            span.FontSize(fontSize.Value);
        }

        if (!string.IsNullOrWhiteSpace(fontColor))
        {
            span.FontColor(fontColor);
        }

        if (isBold)
        {
            span.Bold();
        }

        if (isItalic)
        {
            span.Italic();
        }

        if (isMonospace)
        {
            span.FontFamily("Courier New");
        }

        if (isUnderline)
        {
            span.Underline();
        }
    }

    private void RenderCodeBlock(IContainer container, CodeBlock codeBlock)
    {
        var code = codeBlock.Lines.ToString();
        container
            .Background(Colors.Grey.Lighten3)
            .Padding(8)
            .Text(code)
            .FontFamily("Courier New")
            .FontSize(10);
    }

    private void RenderQuote(IContainer container, QuoteBlock quote)
    {
        container
            .BorderLeft(3)
            .BorderColor(Colors.Blue.Lighten2)
            .PaddingLeft(8)
            .Column(col =>
            {
                col.Spacing(4);

                foreach (var block in quote)
                {
                    col.Item().Element(c => RenderBlock(c, block));
                }
            });
    }
}
