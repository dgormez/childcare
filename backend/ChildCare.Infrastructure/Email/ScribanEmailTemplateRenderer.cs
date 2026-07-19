using System.Collections.Concurrent;
using System.Reflection;
using ChildCare.Application.Common;
using Scriban;

namespace ChildCare.Infrastructure.Email;

/// <summary>
/// Renders a named content template into the shared `_layout.scriban` (feature 020,
/// research.md R1). Content templates are rendered first (to an HTML fragment), then the
/// layout is rendered with `{ content = fragment, locale, footer_text }` — a two-pass render
/// rather than Scriban's `include` mechanism, avoiding a custom `ITemplateLoader` for a single
/// fixed layout. Templates are embedded resources (`Email/Templates/*.scriban`), parsed once
/// and cached — they never change at runtime.
/// </summary>
public class ScribanEmailTemplateRenderer : IEmailTemplateRenderer
{
    private static readonly ConcurrentDictionary<string, Template> ParsedTemplates = new();
    private static readonly Assembly ResourceAssembly = typeof(ScribanEmailTemplateRenderer).Assembly;

    public Task<string> RenderAsync(string templateName, string locale, object model, CancellationToken cancellationToken = default)
    {
        var contentTemplate = GetTemplate(templateName);
        var contentHtml = contentTemplate.Render(model);

        var layoutTemplate = GetTemplate("_layout");
        var layoutModel = new LayoutModel(locale, contentHtml, FooterText: null);
        var html = layoutTemplate.Render(layoutModel);

        return Task.FromResult(html);
    }

    private static Template GetTemplate(string templateName)
    {
        return ParsedTemplates.GetOrAdd(templateName, name =>
        {
            var resourceName = $"{ResourceAssembly.GetName().Name}.Email.Templates.{name}.scriban";
            using var stream = ResourceAssembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Email template '{name}' not found as embedded resource '{resourceName}'.");
            using var reader = new StreamReader(stream);
            var source = reader.ReadToEnd();

            var template = Template.Parse(source, $"{name}.scriban");
            if (template.HasErrors)
                throw new InvalidOperationException($"Email template '{name}' failed to parse: {string.Join("; ", template.Messages)}");

            return template;
        });
    }

    private record LayoutModel(string Locale, string Content, string? FooterText);
}
