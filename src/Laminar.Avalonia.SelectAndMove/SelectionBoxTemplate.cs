using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Metadata;
using Avalonia.Styling;

namespace Laminar.Avalonia.SelectAndMove;

[ControlTemplateScope]
public class SelectionBoxTemplate : ITemplate<Rectangle?>
{
    [Content]
    [TemplateContent]
    public object? Content { get; set; }

    public Rectangle? Build() => TemplateContent.Load(Content)?.Result as Rectangle;

    object? ITemplate.Build() => Build();
}
