using SmkDoc.Application.Modules.Authoring.Templates.Services;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Infrastructure.Engines.DraftHandlers;

public sealed class TemplateDraftHandlerRegistry : ITemplateDraftHandlerRegistry
{
    private readonly Dictionary<TemplateFormat, ITemplateDraftHandler> _handlers;

    public TemplateDraftHandlerRegistry(IEnumerable<ITemplateDraftHandler> handlers)
    {
        _handlers = handlers.ToDictionary(h => h.Format);
    }

    public ITemplateDraftHandler GetHandler(TemplateFormat format)
    {
        if (_handlers.TryGetValue(format, out var handler))
        {
            return handler;
        }

        throw new DomainValidationException($"No draft handler registered for template format: '{format.Name}'.");
    }

    public ITemplateDraftHandler GetHandler(string fileExtension)
    {
        if (!TemplateFormat.TryFromExtension(fileExtension, out var format) || !_handlers.ContainsKey(format))
        {
            throw new DomainValidationException(
                $"Unsupported template file extension: '{fileExtension}'. Allowed formats: .html, .docx, .xlsx");
        }

        return _handlers[format];
    }
}

