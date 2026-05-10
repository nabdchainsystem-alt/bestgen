using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace bestgen.Services.InvoicePdf;

/// <summary>
/// QuestPDF document that renders an <see cref="InvoicePdfModel"/> as an A4
/// PDF. Bilingual: pass a <see cref="System.Globalization.CultureInfo"/> with
/// "ar" prefix to render right-to-left with Arabic labels; otherwise renders
/// English LTR.
/// </summary>
public sealed class InvoicePdfDocument : IDocument
{
    private readonly InvoicePdfModel _model;
    private readonly bool _isArabic;
    private readonly byte[]? _zatcaQrPng;
    private readonly byte[]? _logoBytes;

    private string T(string en, string ar) => _isArabic ? ar : en;

    public InvoicePdfDocument(InvoicePdfModel model, bool isArabic, byte[]? zatcaQrPng, byte[]? logoBytes)
    {
        _model = model;
        _isArabic = isArabic;
        _zatcaQrPng = zatcaQrPng;
        _logoBytes = logoBytes;
    }

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"{_model.DocumentTitle} {_model.DocumentNumber}",
        Author = _model.SellerName,
        Subject = _model.DocumentTitle,
        Producer = "Bestgen ERP",
        Creator = "Bestgen ERP"
    };

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(28);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(t => t
                .FontFamily(InvoicePdfTheme.FontFamily)
                .FontSize(InvoicePdfTheme.BodyFontSize)
                .FontColor(InvoicePdfTheme.Body));

            if (_isArabic)
            {
                page.ContentFromRightToLeft();
            }

            page.Header().Element(ComposeHeader);
            page.Content().Element(ComposeContent);
            page.Footer().Element(ComposeFooter);
        });
    }

    // ============================================================
    // Header — logo + brand on one side, document title + meta on the other
    // ============================================================
    private void ComposeHeader(IContainer container)
    {
        container.PaddingBottom(14).Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                if (_logoBytes is { Length: > 0 })
                {
                    col.Item().Height(56).AlignLeft().Image(_logoBytes).FitHeight();
                    col.Item().PaddingTop(6);
                }
                col.Item().Text(_model.SellerName)
                    .FontFamily(InvoicePdfTheme.FontFamily)
                    .FontSize(14).Bold().FontColor(InvoicePdfTheme.Ink);

                if (!string.IsNullOrWhiteSpace(_model.SellerNameEn) && _model.SellerNameEn != _model.SellerName)
                {
                    col.Item().Text(_model.SellerNameEn).FontSize(10).FontColor(InvoicePdfTheme.Muted);
                }
                col.Item().PaddingTop(2).Text(text =>
                {
                    if (!string.IsNullOrWhiteSpace(_model.SellerAddress))
                        text.Span(_model.SellerAddress + "  ").FontSize(InvoicePdfTheme.SmallFontSize).FontColor(InvoicePdfTheme.Muted);
                    if (!string.IsNullOrWhiteSpace(_model.SellerCity))
                        text.Span(_model.SellerCity + ", ").FontSize(InvoicePdfTheme.SmallFontSize).FontColor(InvoicePdfTheme.Muted);
                    if (!string.IsNullOrWhiteSpace(_model.SellerCountry))
                        text.Span(_model.SellerCountry).FontSize(InvoicePdfTheme.SmallFontSize).FontColor(InvoicePdfTheme.Muted);
                });
                col.Item().Text(text =>
                {
                    if (!string.IsNullOrWhiteSpace(_model.SellerVatNumber))
                    {
                        text.Span(T("VAT: ", "الرقم الضريبي: ")).SemiBold().FontColor(InvoicePdfTheme.Muted).FontSize(InvoicePdfTheme.SmallFontSize);
                        text.Span(_model.SellerVatNumber).FontSize(InvoicePdfTheme.SmallFontSize).FontColor(InvoicePdfTheme.Body);
                        text.Span("    ");
                    }
                    if (!string.IsNullOrWhiteSpace(_model.SellerCommercialRegistration))
                    {
                        text.Span(T("CR: ", "س.ت: ")).SemiBold().FontColor(InvoicePdfTheme.Muted).FontSize(InvoicePdfTheme.SmallFontSize);
                        text.Span(_model.SellerCommercialRegistration).FontSize(InvoicePdfTheme.SmallFontSize).FontColor(InvoicePdfTheme.Body);
                    }
                });
            });

            row.ConstantItem(220).Column(col =>
            {
                col.Item().Background(InvoicePdfTheme.Accent).Padding(14).Column(inner =>
                {
                    inner.Item().Text(_model.DocumentTitle)
                        .FontFamily(InvoicePdfTheme.FontFamily)
                        .FontSize(InvoicePdfTheme.HeaderFontSize)
                        .Bold().FontColor(Colors.White);
                    inner.Item().PaddingTop(6).Text(text =>
                    {
                        text.Span(T("Number: ", "رقم: ")).FontColor("#C5D0DD").FontSize(10);
                        text.Span(_model.DocumentNumber).Bold().FontColor(Colors.White).FontSize(11);
                    });
                    inner.Item().Text(text =>
                    {
                        text.Span(T("Date: ", "التاريخ: ")).FontColor("#C5D0DD").FontSize(10);
                        text.Span(_model.DocumentDate.ToString("yyyy-MM-dd")).FontColor(Colors.White).FontSize(11);
                    });
                    if (!string.IsNullOrWhiteSpace(_model.StatusLabel))
                    {
                        inner.Item().PaddingTop(4)
                            .Background(InvoicePdfTheme.AccentSoft)
                            .PaddingVertical(3).PaddingHorizontal(8)
                            .AlignLeft()
                            .Text(_model.StatusLabel).FontColor(InvoicePdfTheme.Accent).FontSize(9).SemiBold();
                    }
                });
            });
        });
    }

    // ============================================================
    // Content — counterparty info, lines table, totals, notes
    // ============================================================
    private void ComposeContent(IContainer container)
    {
        container.PaddingVertical(8).Column(col =>
        {
            // Counterparty band
            col.Item().Background(InvoicePdfTheme.Soft).Padding(14).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(_model.CounterpartyHeading)
                        .FontSize(9).SemiBold().FontColor(InvoicePdfTheme.Muted);
                    c.Item().PaddingTop(4).Text(_model.CounterpartyName)
                        .FontSize(13).Bold().FontColor(InvoicePdfTheme.Ink);
                    if (!string.IsNullOrWhiteSpace(_model.CounterpartyAddress))
                        c.Item().Text(_model.CounterpartyAddress).FontSize(InvoicePdfTheme.SmallFontSize).FontColor(InvoicePdfTheme.Muted);
                    if (!string.IsNullOrWhiteSpace(_model.CounterpartyPhone))
                        c.Item().Text(_model.CounterpartyPhone).FontSize(InvoicePdfTheme.SmallFontSize).FontColor(InvoicePdfTheme.Muted);
                    if (!string.IsNullOrWhiteSpace(_model.CounterpartyVatNumber))
                    {
                        c.Item().PaddingTop(2).Text(text =>
                        {
                            text.Span(T("VAT: ", "الرقم الضريبي: ")).SemiBold().FontColor(InvoicePdfTheme.Muted).FontSize(InvoicePdfTheme.SmallFontSize);
                            text.Span(_model.CounterpartyVatNumber).FontSize(InvoicePdfTheme.SmallFontSize).FontColor(InvoicePdfTheme.Body);
                        });
                    }
                });

                row.ConstantItem(180).Column(c =>
                {
                    if (!string.IsNullOrWhiteSpace(_model.Warehouse))
                    {
                        c.Item().Text(text =>
                        {
                            text.Span(T("Warehouse: ", "المستودع: ")).SemiBold().FontColor(InvoicePdfTheme.Muted).FontSize(InvoicePdfTheme.SmallFontSize);
                            text.Span(_model.Warehouse).FontSize(InvoicePdfTheme.SmallFontSize).FontColor(InvoicePdfTheme.Body);
                        });
                    }
                    if (!string.IsNullOrWhiteSpace(_model.PaymentMethod))
                    {
                        c.Item().Text(text =>
                        {
                            text.Span(T("Payment: ", "الدفع: ")).SemiBold().FontColor(InvoicePdfTheme.Muted).FontSize(InvoicePdfTheme.SmallFontSize);
                            text.Span(_model.PaymentMethod).FontSize(InvoicePdfTheme.SmallFontSize).FontColor(InvoicePdfTheme.Body);
                        });
                    }
                });
            });

            // Lines table
            col.Item().PaddingTop(14).Element(ComposeLinesTable);

            // Totals + ZATCA QR side-by-side
            col.Item().PaddingTop(14).Row(row =>
            {
                // Left: Notes + ZATCA QR
                row.RelativeItem().Column(c =>
                {
                    if (!string.IsNullOrWhiteSpace(_model.Notes))
                    {
                        c.Item().Text(T("Notes", "ملاحظات")).SemiBold().FontColor(InvoicePdfTheme.Muted).FontSize(9);
                        c.Item().PaddingTop(2).Text(_model.Notes).FontSize(InvoicePdfTheme.SmallFontSize).FontColor(InvoicePdfTheme.Body);
                    }
                    if (_zatcaQrPng is { Length: > 0 })
                    {
                        c.Item().PaddingTop(_model.Notes is { Length: > 0 } ? 12 : 0)
                            .Width(120).Height(120).Image(_zatcaQrPng).FitArea();
                        c.Item().Width(120).PaddingTop(4).AlignCenter()
                            .Text(T("ZATCA Phase 1", "هيئة الزكاة — المرحلة الأولى"))
                            .FontColor(InvoicePdfTheme.Faint).FontSize(8);
                    }
                });

                // Right: Totals
                row.ConstantItem(240).Column(c =>
                {
                    c.Item().Element(TotalsBlock);
                });
            });
        });
    }

    private void ComposeLinesTable(IContainer container)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(cd =>
            {
                cd.ConstantColumn(28);   // #
                cd.RelativeColumn(4);    // Item
                cd.ConstantColumn(50);   // Qty
                cd.ConstantColumn(70);   // Unit Price
                cd.ConstantColumn(60);   // Disc
                cd.ConstantColumn(50);   // VAT %
                cd.ConstantColumn(80);   // Total
            });

            table.Header(h =>
            {
                static IContainer Cell(IContainer c) =>
                    c.Background(InvoicePdfTheme.Accent).PaddingVertical(6).PaddingHorizontal(6);

                h.Cell().Element(Cell).Text("#").FontColor(Colors.White).Bold().FontSize(InvoicePdfTheme.TableHeaderFontSize);
                h.Cell().Element(Cell).Text(T("Item", "الصنف")).FontColor(Colors.White).Bold().FontSize(InvoicePdfTheme.TableHeaderFontSize);
                h.Cell().Element(Cell).AlignRight().Text(T("Qty", "الكمية")).FontColor(Colors.White).Bold().FontSize(InvoicePdfTheme.TableHeaderFontSize);
                h.Cell().Element(Cell).AlignRight().Text(T("Unit Price", "السعر")).FontColor(Colors.White).Bold().FontSize(InvoicePdfTheme.TableHeaderFontSize);
                h.Cell().Element(Cell).AlignRight().Text(T("Discount", "الخصم")).FontColor(Colors.White).Bold().FontSize(InvoicePdfTheme.TableHeaderFontSize);
                h.Cell().Element(Cell).AlignRight().Text(T("VAT %", "الضريبة %")).FontColor(Colors.White).Bold().FontSize(InvoicePdfTheme.TableHeaderFontSize);
                h.Cell().Element(Cell).AlignRight().Text(T("Total", "الإجمالي")).FontColor(Colors.White).Bold().FontSize(InvoicePdfTheme.TableHeaderFontSize);
            });

            for (int i = 0; i < _model.Lines.Count; i++)
            {
                var line = _model.Lines[i];
                var bg = i % 2 == 0 ? InvoicePdfTheme.Surface : InvoicePdfTheme.Soft;

                IContainer Cell(IContainer c) => c.Background(bg).PaddingVertical(6).PaddingHorizontal(6).BorderBottom(0.5f).BorderColor(InvoicePdfTheme.Line);

                table.Cell().Element(Cell).Text((i + 1).ToString()).FontColor(InvoicePdfTheme.Muted);
                table.Cell().Element(Cell).Column(c =>
                {
                    c.Item().Text(line.ProductName).FontColor(InvoicePdfTheme.Ink).SemiBold();
                    if (!string.IsNullOrWhiteSpace(line.ProductSku))
                        c.Item().Text(line.ProductSku).FontColor(InvoicePdfTheme.Faint).FontSize(InvoicePdfTheme.SmallFontSize);
                });
                table.Cell().Element(Cell).AlignRight().Text(line.Quantity.ToString("N2"));
                table.Cell().Element(Cell).AlignRight().Text(line.UnitPrice.ToString("N2"));
                table.Cell().Element(Cell).AlignRight().Text(line.Discount.ToString("N2"));
                table.Cell().Element(Cell).AlignRight().Text(line.VatRate.ToString("N2") + "%");
                table.Cell().Element(Cell).AlignRight().Text(line.LineTotal.ToString("N2")).SemiBold().FontColor(InvoicePdfTheme.Ink);
            }
        });
    }

    private void TotalsBlock(IContainer container)
    {
        container.Background(InvoicePdfTheme.Soft).Padding(14).Column(col =>
        {
            void Row(string label, decimal value, bool emphasize = false)
            {
                col.Item().Row(r =>
                {
                    r.RelativeItem().Text(label).FontColor(emphasize ? InvoicePdfTheme.Ink : InvoicePdfTheme.Muted)
                        .FontSize(emphasize ? InvoicePdfTheme.TotalFontSize : InvoicePdfTheme.BodyFontSize)
                        .SemiBold();
                    r.RelativeItem().AlignRight().Text(InvoicePdfTheme.Money(value, _model.CurrencySymbol))
                        .FontColor(emphasize ? InvoicePdfTheme.Accent : InvoicePdfTheme.Body)
                        .FontSize(emphasize ? InvoicePdfTheme.TotalFontSize : InvoicePdfTheme.BodyFontSize)
                        .SemiBold();
                });
            }
            Row(T("Subtotal", "المجموع قبل الضريبة"), _model.Subtotal);
            if (_model.DiscountTotal != 0)
                Row(T("Discount", "إجمالي الخصم"), _model.DiscountTotal);
            Row(T("VAT", "ضريبة القيمة المضافة"), _model.VatTotal);
            col.Item().PaddingTop(8).BorderTop(1).BorderColor(InvoicePdfTheme.Accent);
            col.Item().PaddingTop(8);
            Row(T("Grand Total", "الإجمالي النهائي"), _model.GrandTotal, emphasize: true);
        });
    }

    // ============================================================
    // Footer — terms / footer text + page number
    // ============================================================
    private void ComposeFooter(IContainer container)
    {
        container.PaddingTop(8).BorderTop(0.5f).BorderColor(InvoicePdfTheme.Line)
            .PaddingTop(6).Row(row =>
        {
            row.RelativeItem().Text(_model.FooterText ?? string.Empty)
                .FontSize(InvoicePdfTheme.SmallFontSize)
                .FontColor(InvoicePdfTheme.Muted);

            row.ConstantItem(120).AlignRight().Text(text =>
            {
                text.Span(T("Page ", "صفحة ")).FontColor(InvoicePdfTheme.Faint).FontSize(InvoicePdfTheme.SmallFontSize);
                text.CurrentPageNumber().FontColor(InvoicePdfTheme.Muted).FontSize(InvoicePdfTheme.SmallFontSize);
                text.Span(T(" / ", " / ")).FontColor(InvoicePdfTheme.Faint).FontSize(InvoicePdfTheme.SmallFontSize);
                text.TotalPages().FontColor(InvoicePdfTheme.Muted).FontSize(InvoicePdfTheme.SmallFontSize);
            });
        });
    }
}
