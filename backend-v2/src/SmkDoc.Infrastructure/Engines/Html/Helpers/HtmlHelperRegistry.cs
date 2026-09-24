using HandlebarsDotNet;
using SmkDoc.Application.Common.Helpers;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Infrastructure.Engines.Html.Helpers;

public class HtmlHelperRegistry(IMediaGenerationService mediaService) : IHtmlHelperRegistry
{
    public void RegisterHelpers(IHandlebars handlebars)
    {
        // 1. Thai Transforms & Formatters
        handlebars.RegisterHelper("thaitransform", (output, context, arguments) =>
        {
            if (arguments.Length >= 2)
            {
                var value = arguments[0]?.ToString();
                var transformType = arguments[1]?.ToString();
                
                if (value != null && !string.IsNullOrWhiteSpace(transformType))
                {
                    output.WriteSafeString(ThaiDataTransformer.Transform(value, transformType));
                    return;
                }
                
                if (value != null)
                {
                    output.WriteSafeString(value);
                    return;
                }
            }
            output.WriteSafeString(string.Empty);
        });

        handlebars.RegisterHelper("thai_baht_text", (output, context, arguments) =>
        {
            if (arguments.Length >= 1 && arguments[0] != null)
            {
                output.WriteSafeString(ThaiDataTransformer.Transform(arguments[0]?.ToString() ?? string.Empty, "thai_baht_text"));
                return;
            }
            output.WriteSafeString(string.Empty);
        });

        handlebars.RegisterHelper("thai_date", (output, context, arguments) =>
        {
            if (arguments.Length >= 1 && arguments[0] != null)
            {
                output.WriteSafeString(ThaiDataTransformer.Transform(arguments[0]?.ToString() ?? string.Empty, "thai_date"));
                return;
            }
            output.WriteSafeString(string.Empty);
        });

        handlebars.RegisterHelper("format_number", (output, context, arguments) =>
        {
            if (arguments.Length >= 1 && arguments[0] != null)
            {
                output.WriteSafeString(ThaiDataTransformer.Transform(arguments[0]?.ToString() ?? string.Empty, "number"));
                return;
            }
            output.WriteSafeString(string.Empty);
        });

        // 2. Loop Indexing & Arithmetic
        handlebars.RegisterHelper("addOne", (output, context, arguments) =>
        {
            if (arguments.Length >= 1 && int.TryParse(arguments[0]?.ToString(), out int val))
            {
                output.WriteSafeString((val + 1).ToString());
                return;
            }
            output.WriteSafeString("1");
        });

        handlebars.RegisterHelper("inc", (output, context, arguments) =>
        {
            if (arguments.Length >= 1 && int.TryParse(arguments[0]?.ToString(), out int val))
            {
                output.WriteSafeString((val + 1).ToString());
                return;
            }
            output.WriteSafeString("1");
        });

        // 3. Logic & Comparison Block Helpers
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

        // 4. Dynamic Media Helpers (QR Code & Barcode natively supported in loops!)
        handlebars.RegisterHelper("qr", (output, context, arguments) =>
        {
            if (arguments.Length >= 1 && arguments[0] != null)
            {
                string text = arguments[0]?.ToString() ?? string.Empty;
                string dataUri = mediaService.GenerateQrCodeDataUri(text, 150, 150);
                output.WriteSafeString($"<img src=\"{dataUri}\" style=\"width:150px;height:150px;display:block;\" />");
                return;
            }
            output.WriteSafeString(string.Empty);
        });

        handlebars.RegisterHelper("qrcode", (output, context, arguments) =>
        {
            if (arguments.Length >= 1 && arguments[0] != null)
            {
                string text = arguments[0]?.ToString() ?? string.Empty;
                string dataUri = mediaService.GenerateQrCodeDataUri(text, 150, 150);
                output.WriteSafeString($"<img src=\"{dataUri}\" style=\"width:150px;height:150px;display:block;\" />");
                return;
            }
            output.WriteSafeString(string.Empty);
        });

        handlebars.RegisterHelper("barcode", (output, context, arguments) =>
        {
            if (arguments.Length >= 1 && arguments[0] != null)
            {
                string text = arguments[0]?.ToString() ?? string.Empty;
                string dataUri = mediaService.GenerateBarcodeDataUri(text, 300, 100);
                output.WriteSafeString($"<img src=\"{dataUri}\" style=\"width:300px;height:100px;display:block;\" />");
                return;
            }
            output.WriteSafeString(string.Empty);
        });
    }
}
