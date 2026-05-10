using bestgen.ViewModels;

namespace bestgen.Services;

public class InvoiceCalculationService
{
    public void CalculateSales(SalesInvoiceFormViewModel invoice)
    {
        invoice.Items = invoice.Items
            .Where(item => item.ProductId > 0 && item.Quantity > 0)
            .ToList();

        foreach (var item in invoice.Items)
        {
            var beforeDiscount = item.Quantity * item.UnitPrice;
            var taxableAmount = Math.Max(0, beforeDiscount - item.Discount);
            var vatAmount = taxableAmount * item.VatRate / 100;
            item.LineTotal = taxableAmount + vatAmount;
        }

        invoice.Subtotal = invoice.Items.Sum(item => item.Quantity * item.UnitPrice);
        invoice.DiscountTotal = invoice.Items.Sum(item => item.Discount);
        invoice.VatTotal = invoice.Items.Sum(item => Math.Max(0, item.Quantity * item.UnitPrice - item.Discount) * item.VatRate / 100);
        invoice.GrandTotal = invoice.Subtotal - invoice.DiscountTotal + invoice.VatTotal;
        invoice.RemainingAmount = Math.Max(0, invoice.GrandTotal - invoice.PaidAmount);
    }

    public void CalculateQuotation(SalesQuotationFormViewModel quotation)
    {
        quotation.Items = quotation.Items
            .Where(item => item.ProductId > 0 && item.Quantity > 0)
            .ToList();

        foreach (var item in quotation.Items)
        {
            var beforeDiscount = item.Quantity * item.UnitPrice;
            var taxableAmount = Math.Max(0, beforeDiscount - item.Discount);
            var vatAmount = taxableAmount * item.VatRate / 100;
            item.LineTotal = taxableAmount + vatAmount;
        }

        quotation.Subtotal = quotation.Items.Sum(item => item.Quantity * item.UnitPrice);
        quotation.DiscountTotal = quotation.Items.Sum(item => item.Discount);
        quotation.VatTotal = quotation.Items.Sum(item => Math.Max(0, item.Quantity * item.UnitPrice - item.Discount) * item.VatRate / 100);
        quotation.GrandTotal = quotation.Subtotal - quotation.DiscountTotal + quotation.VatTotal;
    }

    public void CalculatePurchase(PurchaseInvoiceFormViewModel invoice)
    {
        invoice.Items = invoice.Items
            .Where(item => item.ProductId > 0 && item.Quantity > 0)
            .ToList();

        foreach (var item in invoice.Items)
        {
            var beforeDiscount = item.Quantity * item.UnitCost;
            var taxableAmount = Math.Max(0, beforeDiscount - item.Discount);
            var vatAmount = taxableAmount * item.VatRate / 100;
            item.LineTotal = taxableAmount + vatAmount;
        }

        invoice.Subtotal = invoice.Items.Sum(item => item.Quantity * item.UnitCost);
        invoice.DiscountTotal = invoice.Items.Sum(item => item.Discount);
        invoice.VatTotal = invoice.Items.Sum(item => Math.Max(0, item.Quantity * item.UnitCost - item.Discount) * item.VatRate / 100);
        invoice.GrandTotal = invoice.Subtotal - invoice.DiscountTotal + invoice.VatTotal;
        invoice.RemainingAmount = Math.Max(0, invoice.GrandTotal - invoice.PaidAmount);
    }
}
