using HandlebarsDotNet;

namespace SmkDoc.Infrastructure.Engines.Html.Helpers;

public interface IHtmlHelperRegistry
{
    void RegisterHelpers(IHandlebars handlebars);
}
