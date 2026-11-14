using System.Globalization;
using HandlebarsDotNet;
using SmkDoc.Application.Common.Helpers;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Infrastructure.Engines.Html.Helpers;

public class HtmlHelperRegistry(IMediaGenerationService mediaService) : IHtmlHelperRegistry
{
    public void RegisterHelpers(IHandlebars handlebars)
    {
        RegisterThaiTransformHelpers(handlebars);
        RegisterArithmeticHelpers(handlebars);
        RegisterLogicHelpers(handlebars);
        RegisterMediaHelpers(handlebars, mediaService);
    }

    private static void RegisterThaiTransformHelpers(IHandlebars handlebars)
    {
        handlebars.RegisterHelper("thaitransform", (output, _, arguments) =>
        {
            if (arguments.Length < 2 || arguments[0] == null)
            {
                output.WriteSafeString(string.Empty);
                return;
            }

            var value = arguments[0]!.ToString()!;
            var transformType = arguments[1]?.ToString();
            output.WriteSafeString(!string.IsNullOrWhiteSpace(transformType)
                ? ThaiDataTransformer.Transform(value, transformType)
                : value);
        });

        RegisterSingleArgTransformHelper(handlebars, "thai_baht_text", "thai_baht_text");
        RegisterSingleArgTransformHelper(handlebars, "thai_date", "thai_date");
        RegisterSingleArgTransformHelper(handlebars, "format_number", "number");
    }

    private static void RegisterSingleArgTransformHelper(IHandlebars handlebars, string helperName, string transformName)
    {
        handlebars.RegisterHelper(helperName, (output, _, arguments) =>
        {
            if (arguments.Length >= 1 && arguments[0] != null)
            {
                output.WriteSafeString(ThaiDataTransformer.Transform(arguments[0]?.ToString() ?? string.Empty, transformName));
                return;
            }
            output.WriteSafeString(string.Empty);
        });
    }

    private static void RegisterArithmeticHelpers(IHandlebars handlebars)
    {
        RegisterIncrementHelper(handlebars, "addOne");
        RegisterIncrementHelper(handlebars, "inc");
    }

    private static void RegisterIncrementHelper(IHandlebars handlebars, string helperName)
    {
        handlebars.RegisterHelper(helperName, (output, _, arguments) =>
        {
            if (arguments.Length >= 1 && int.TryParse(arguments[0]?.ToString(), out int val))
            {
                output.WriteSafeString((val + 1).ToString(CultureInfo.InvariantCulture));
                return;
            }
            output.WriteSafeString("1");
        });
    }

    private static void RegisterLogicHelpers(IHandlebars handlebars)
    {
        handlebars.RegisterHelper("ifEquals", (output, options, context, arguments) =>
        {
            if (arguments.Length >= 2 && string.Equals(arguments[0]?.ToString(), arguments[1]?.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                options.Template(output, context);
            }
            else
            {
                options.Inverse(output, context);
            }
        });
    }

    private static void RegisterMediaHelpers(IHandlebars handlebars, IMediaGenerationService mediaService)
    {
        RegisterMediaImageHelper(handlebars, "qr", text => mediaService.GenerateQrCodeDataUri(text, 150, 150), 150, 150);
        RegisterMediaImageHelper(handlebars, "qrcode", text => mediaService.GenerateQrCodeDataUri(text, 150, 150), 150, 150);
        RegisterMediaImageHelper(handlebars, "barcode", text => mediaService.GenerateBarcodeDataUri(text, 300, 100), 300, 100);
    }

    private static void RegisterMediaImageHelper(
        IHandlebars handlebars,
        string helperName,
        Func<string, Uri> generateDataUri,
        int widthPx,
        int heightPx)
    {
        handlebars.RegisterHelper(helperName, (output, _, arguments) =>
        {
            if (arguments.Length >= 1 && arguments[0] != null)
            {
                string text = arguments[0]?.ToString() ?? string.Empty;
                Uri dataUri = generateDataUri(text);
                output.WriteSafeString($"<img src=\"{dataUri.OriginalString}\" style=\"width:{widthPx}px;height:{heightPx}px;display:block;\" />");
                return;
            }
            output.WriteSafeString(string.Empty);
        });
    }
}
