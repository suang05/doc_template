using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SmkDoc.Application.Common.Helpers;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Infrastructure.Imaging;
using A   = DocumentFormat.OpenXml.Drawing;
using DW  = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using static SmkDoc.Application.Common.Helpers.PlaceholderHelper;

namespace SmkDoc.Infrastructure.Engines.Word;

public class WordMediaInjector
{
    private readonly IMediaGenerationService _mediaService;
    private uint _drawingId = 1000;

    public WordMediaInjector(IMediaGenerationService mediaService)
    {
        _mediaService = mediaService;
    }

    /// <summary>
    /// Scans all paragraphs for {{qr:key}} and {{barcode:key}}, generates images from
    /// the corresponding value in <paramref name="data"/>, and embeds them inline.
    /// Text placeholders are left untouched for WordTextReplacer to handle.
    /// </summary>
    public void InjectMedia(MainDocumentPart mainPart, Dictionary<string, string> data)
    {
        foreach (var p in CollectParagraphs(mainPart))
        {
            if (!p.InnerText.Contains("{{")) continue;

            foreach (System.Text.RegularExpressions.Match m in PlaceholderHelper.Pattern.Matches(p.InnerText))
            {
                var parsed = PlaceholderHelper.Parse(m.Groups[1].Value);
                byte[]? imageBytes = null;
                int w = 0, h = 0;

                if (parsed.Kind == PlaceholderKind.Qr && data.TryGetValue(parsed.Key, out var qrText))
                {
                    w = h = 150;
                    imageBytes = _mediaService.GenerateQrCode(qrText, w, h);
                }
                else if (parsed.Kind == PlaceholderKind.Barcode && data.TryGetValue(parsed.Key, out var bcText))
                {
                    w = 300; h = 100;
                    imageBytes = _mediaService.GenerateBarcode(bcText, w, h);
                }

                if (imageBytes is not null)
                {
                    ReplaceMarkerWithImage(mainPart, p, m.Value, imageBytes, w, h);
                }
            }
        }
    }

    private void ReplaceMarkerWithImage(MainDocumentPart mainPart, Paragraph p, string marker, byte[] imageBytes, int widthPx, int heightPx)
    {
        if (!p.InnerText.Contains(marker)) return;

        var imagePart = mainPart.AddImagePart(ImagePartType.Png);
        using (var stream = new MemoryStream(imageBytes))
            imagePart.FeedData(stream);

        string relId = mainPart.GetIdOfPart(imagePart);

        p.RemoveAllChildren<Run>();
        p.AppendChild(new Run(BuildDrawing(relId, ++_drawingId, widthPx, heightPx)));
    }

    private static IEnumerable<Paragraph> CollectParagraphs(MainDocumentPart mainPart)
    {
        var body = mainPart.Document.Body?.Descendants<Paragraph>() ?? [];
        var headers = mainPart.HeaderParts.SelectMany(h => h.Header.Descendants<Paragraph>());
        var footers = mainPart.FooterParts.SelectMany(f => f.Footer.Descendants<Paragraph>());
        return body.Concat(headers).Concat(footers);
    }

    private static Drawing BuildDrawing(string relationshipId, uint id, int widthPx, int heightPx)
    {
        long cx = (long)widthPx  * 9525;   // 1 px = 9525 EMU at 96 DPI
        long cy = (long)heightPx * 9525;

        return new Drawing(
            new DW.Inline(
                new DW.Extent { Cx = cx, Cy = cy },
                new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                new DW.DocProperties { Id = id, Name = $"Picture {id}" },
                new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(
                    new A.GraphicData(
                        new PIC.Picture(
                            new PIC.NonVisualPictureProperties(
                                new PIC.NonVisualDrawingProperties { Id = id, Name = $"Image {id}" },
                                new PIC.NonVisualPictureDrawingProperties()
                            ),
                            new PIC.BlipFill(
                                new A.Blip { Embed = relationshipId },
                                new A.Stretch(new A.FillRectangle())
                            ),
                            new PIC.ShapeProperties(
                                new A.Transform2D(
                                    new A.Offset { X = 0L, Y = 0L },
                                    new A.Extents { Cx = cx, Cy = cy }
                                ),
                                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }
                            )
                        )
                    ) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }
                )
            )
        );
    }
}
