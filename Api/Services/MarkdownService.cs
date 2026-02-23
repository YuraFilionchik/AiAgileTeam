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
        var inlineText = BuildInlineText(paragraph.Inline);
        container.Text(inlineText);
    }

    private string BuildInlineText(Inline? inline)
    {
        if (inline == null) return "";

        var result = new System.Text.StringBuilder();
        var current = inline;

        while (current != null)
        {
            switch (current)
            {
                case LiteralInline literal:
                    result.Append(literal.Content.Text);
                    break;

                case EmphasisInline emphasis:
                    var content = BuildInlineText(emphasis.FirstChild);
                    result.Append($"*{content}*");
                    break;

                case CodeInline code:
                    result.Append($"`{code.Content}`");
                    break;

                case LinkInline link:
                    var linkText = BuildInlineText(link.FirstChild);
                    result.Append(link.Url != null ? $"{linkText} ({link.Url})" : linkText);
                    break;

                case LineBreakInline:
                    result.Append("\n");
                    break;

                case ContainerInline containerInline:
                    var child = containerInline.FirstChild;
                    while (child != null)
                    {
                        result.Append(BuildInlineText(child));
                        child = child.NextSibling;
                    }
                    break;
            }

            current = current.NextSibling;
        }

        return result.ToString();
    }

    private void RenderHeading(IContainer container, HeadingBlock heading)
    {
        var inlineText = BuildInlineText(heading.Inline);
        var fontSize = heading.Level switch
        {
            1 => 24,
            2 => 20,
            3 => 16,
            4 => 14,
            _ => 12
        };

        container
            .Text(inlineText)
            .Bold()
            .FontSize(fontSize)
            .FontColor(Colors.Blue.Medium);
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
                                    var inlineText = BuildInlineText(p.Inline);
                                    itemCol.Item().Text(inlineText);
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
