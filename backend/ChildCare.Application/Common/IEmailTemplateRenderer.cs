namespace ChildCare.Application.Common;

/// <summary>
/// Renders a named content template into the shared HTML email layout (feature 020,
/// research.md R1). <paramref name="model"/> is rendered via reflection (public property names
/// used as-is in the template, no case conversion) — callers MUST HTML-encode any untrusted
/// free-text field (e.g. a director's typed subject/body) before including it in the model,
/// since Scriban does not auto-escape interpolated values.
/// </summary>
public interface IEmailTemplateRenderer
{
    Task<string> RenderAsync(string templateName, string locale, object model, CancellationToken cancellationToken = default);
}
