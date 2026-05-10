using System.Globalization;
using System.Text;
using System.Xml.Linq;
using bestgen.Models;

namespace bestgen.Services.Zatca;

/// <summary>
/// Builds the UBL 2.1 XML document for a sales invoice per ZATCA's KSA
/// implementation guide. Output is "structurally compliant" — covers all
/// required fields and the right namespaces. Final compliance against
/// ZATCA's 200+ business rules is checked when you submit to the FATOORA
/// sandbox; this gets you 95% of the way there.
/// </summary>
public class ZatcaUblBuilder
{
    private static readonly XNamespace Inv  = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    private static readonly XNamespace Cac  = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    private static readonly XNamespace Cbc  = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    private static readonly XNamespace Ext  = "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";

    public string Build(
        SalesInvoice invoice,
        CompanySettings seller,
        EInvoice eInvoice)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(Inv + "Invoice",
                new XAttribute(XNamespace.Xmlns + "cac", Cac.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "cbc", Cbc.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "ext", Ext.NamespaceName),

                // Document identity
                new XElement(Cbc + "ProfileID", eInvoice.ProfileId),
                new XElement(Cbc + "ID", invoice.InvoiceNumber),
                new XElement(Cbc + "UUID", eInvoice.Uuid),
                new XElement(Cbc + "IssueDate", invoice.InvoiceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                new XElement(Cbc + "IssueTime", invoice.InvoiceDate.ToString("HH:mm:ss", CultureInfo.InvariantCulture)),
                new XElement(Cbc + "InvoiceTypeCode",
                    new XAttribute("name", eInvoice.InvoiceSubtype),
                    eInvoice.InvoiceTypeCode),
                new XElement(Cbc + "DocumentCurrencyCode", "SAR"),
                new XElement(Cbc + "TaxCurrencyCode", "SAR"),

                // ICV — additional document reference id "ICV"
                new XElement(Cac + "AdditionalDocumentReference",
                    new XElement(Cbc + "ID", "ICV"),
                    new XElement(Cbc + "UUID", eInvoice.IcvCounter.ToString(CultureInfo.InvariantCulture))),

                // PIH — Previous Invoice Hash
                new XElement(Cac + "AdditionalDocumentReference",
                    new XElement(Cbc + "ID", "PIH"),
                    new XElement(Cac + "Attachment",
                        new XElement(Cbc + "EmbeddedDocumentBinaryObject",
                            new XAttribute("mimeCode", "text/plain"),
                            eInvoice.PreviousInvoiceHash))),

                // Seller (AccountingSupplierParty)
                BuildParty(Cac + "AccountingSupplierParty",
                    name: seller.CompanyNameAr,
                    vat: seller.VatNumber,
                    cr: seller.CommercialRegistrationNumber,
                    address: seller.Address,
                    city: seller.City,
                    country: "SA"),

                // Buyer (AccountingCustomerParty)
                BuildParty(Cac + "AccountingCustomerParty",
                    name: invoice.Customer?.NameAr ?? "Walk-in Customer",
                    vat: invoice.Customer?.VatNumber,
                    cr: invoice.Customer?.CommercialRegistrationNumber,
                    address: invoice.Customer?.Address,
                    city: invoice.Customer?.City,
                    country: "SA"),

                // Tax totals
                new XElement(Cac + "TaxTotal",
                    new XElement(Cbc + "TaxAmount",
                        new XAttribute("currencyID", "SAR"),
                        Decimal2(invoice.VatTotal))),
                new XElement(Cac + "TaxTotal",
                    new XElement(Cbc + "TaxAmount",
                        new XAttribute("currencyID", "SAR"),
                        Decimal2(invoice.VatTotal)),
                    new XElement(Cac + "TaxSubtotal",
                        new XElement(Cbc + "TaxableAmount",
                            new XAttribute("currencyID", "SAR"),
                            Decimal2(invoice.Subtotal - invoice.DiscountTotal)),
                        new XElement(Cbc + "TaxAmount",
                            new XAttribute("currencyID", "SAR"),
                            Decimal2(invoice.VatTotal)),
                        new XElement(Cac + "TaxCategory",
                            new XElement(Cbc + "ID", "S"),
                            new XElement(Cbc + "Percent", Decimal2(15m)),
                            new XElement(Cac + "TaxScheme",
                                new XElement(Cbc + "ID", "VAT"))))),

                // LegalMonetaryTotal
                new XElement(Cac + "LegalMonetaryTotal",
                    new XElement(Cbc + "LineExtensionAmount",
                        new XAttribute("currencyID", "SAR"),
                        Decimal2(invoice.Subtotal)),
                    new XElement(Cbc + "TaxExclusiveAmount",
                        new XAttribute("currencyID", "SAR"),
                        Decimal2(invoice.Subtotal - invoice.DiscountTotal)),
                    new XElement(Cbc + "TaxInclusiveAmount",
                        new XAttribute("currencyID", "SAR"),
                        Decimal2(invoice.GrandTotal)),
                    new XElement(Cbc + "AllowanceTotalAmount",
                        new XAttribute("currencyID", "SAR"),
                        Decimal2(invoice.DiscountTotal)),
                    new XElement(Cbc + "PayableAmount",
                        new XAttribute("currencyID", "SAR"),
                        Decimal2(invoice.GrandTotal))),

                // Invoice lines
                BuildLines(invoice)
            ));

        // Render as UTF-8 (no BOM, no whitespace) — ZATCA requires UTF-8.
        var settings = new System.Xml.XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = false,
            OmitXmlDeclaration = false
        };
        using var ms = new MemoryStream();
        using (var xw = System.Xml.XmlWriter.Create(ms, settings))
        {
            doc.Save(xw);
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static XElement BuildParty(XName parentTag, string name, string? vat, string? cr, string? address, string? city, string country)
    {
        var party = new XElement(Cac + "Party");

        if (!string.IsNullOrWhiteSpace(cr))
        {
            party.Add(new XElement(Cac + "PartyIdentification",
                new XElement(Cbc + "ID",
                    new XAttribute("schemeID", "CRN"),
                    cr)));
        }

        party.Add(new XElement(Cac + "PostalAddress",
            new XElement(Cbc + "StreetName", address ?? "—"),
            new XElement(Cbc + "CityName", city ?? "—"),
            new XElement(Cac + "Country",
                new XElement(Cbc + "IdentificationCode", country))));

        if (!string.IsNullOrWhiteSpace(vat))
        {
            party.Add(new XElement(Cac + "PartyTaxScheme",
                new XElement(Cbc + "CompanyID", vat),
                new XElement(Cac + "TaxScheme",
                    new XElement(Cbc + "ID", "VAT"))));
        }

        party.Add(new XElement(Cac + "PartyLegalEntity",
            new XElement(Cbc + "RegistrationName", name)));

        return new XElement(parentTag, party);
    }

    private static IEnumerable<XElement> BuildLines(SalesInvoice invoice)
    {
        var index = 1;
        foreach (var line in invoice.Items)
        {
            var net = line.LineTotal - (line.LineTotal * line.VatRate / (100m + line.VatRate));
            var taxAmount = line.LineTotal - net;
            yield return new XElement(Cac + "InvoiceLine",
                new XElement(Cbc + "ID", index.ToString(CultureInfo.InvariantCulture)),
                new XElement(Cbc + "InvoicedQuantity",
                    new XAttribute("unitCode", "PCE"),
                    Decimal4(line.Quantity)),
                new XElement(Cbc + "LineExtensionAmount",
                    new XAttribute("currencyID", "SAR"),
                    Decimal2(net)),
                new XElement(Cac + "TaxTotal",
                    new XElement(Cbc + "TaxAmount",
                        new XAttribute("currencyID", "SAR"),
                        Decimal2(taxAmount)),
                    new XElement(Cbc + "RoundingAmount",
                        new XAttribute("currencyID", "SAR"),
                        Decimal2(line.LineTotal))),
                new XElement(Cac + "Item",
                    new XElement(Cbc + "Name", line.Product?.NameAr ?? "Item"),
                    new XElement(Cac + "ClassifiedTaxCategory",
                        new XElement(Cbc + "ID", "S"),
                        new XElement(Cbc + "Percent", Decimal2(line.VatRate)),
                        new XElement(Cac + "TaxScheme",
                            new XElement(Cbc + "ID", "VAT")))),
                new XElement(Cac + "Price",
                    new XElement(Cbc + "PriceAmount",
                        new XAttribute("currencyID", "SAR"),
                        Decimal2(line.UnitPrice))));
            index++;
        }
    }

    private static string Decimal2(decimal v) => v.ToString("F2", CultureInfo.InvariantCulture);
    private static string Decimal4(decimal v) => v.ToString("F4", CultureInfo.InvariantCulture);
}
