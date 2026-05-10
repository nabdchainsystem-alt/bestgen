using bestgen.Services;
using bestgen.ViewModels;
using FluentAssertions;
using Xunit;

namespace Bestgen.Tests;

public class InvoiceCalculationServiceTests
{
    private readonly InvoiceCalculationService _svc = new();

    [Fact]
    public void Single_line_15pct_vat()
    {
        var vm = new SalesInvoiceFormViewModel
        {
            Items = new()
            {
                new() { ProductId = 1, Quantity = 2, UnitPrice = 100m, Discount = 0m, VatRate = 15m }
            }
        };
        _svc.CalculateSales(vm);

        vm.Subtotal.Should().Be(200m);
        vm.VatTotal.Should().Be(30m);
        vm.GrandTotal.Should().Be(230m);
    }

    [Fact]
    public void Discount_reduces_taxable_base_before_vat()
    {
        var vm = new SalesInvoiceFormViewModel
        {
            Items = new()
            {
                new() { ProductId = 1, Quantity = 1, UnitPrice = 1000m, Discount = 100m, VatRate = 15m }
            }
        };
        _svc.CalculateSales(vm);

        // taxable = 1000 - 100 = 900; VAT = 135; grand = 1035
        vm.VatTotal.Should().Be(135m);
        vm.GrandTotal.Should().Be(1035m);
    }

    [Fact]
    public void Items_with_zero_quantity_are_dropped()
    {
        var vm = new SalesInvoiceFormViewModel
        {
            Items = new()
            {
                new() { ProductId = 1, Quantity = 1, UnitPrice = 100m, VatRate = 15m },
                new() { ProductId = 2, Quantity = 0, UnitPrice = 999m, VatRate = 15m },
                new() { ProductId = 0, Quantity = 1, UnitPrice = 50m,  VatRate = 15m }, // ProductId = 0 also skipped
            }
        };
        _svc.CalculateSales(vm);

        vm.Items.Should().HaveCount(1);
        vm.GrandTotal.Should().Be(115m);
    }

    [Fact]
    public void Remaining_amount_clamped_to_zero_when_overpaid()
    {
        var vm = new SalesInvoiceFormViewModel
        {
            PaidAmount = 500m,
            Items = new()
            {
                new() { ProductId = 1, Quantity = 1, UnitPrice = 100m, VatRate = 15m }
            }
        };
        _svc.CalculateSales(vm);

        vm.GrandTotal.Should().Be(115m);
        vm.RemainingAmount.Should().Be(0m); // not -385
    }

    [Fact]
    public void Multi_line_with_mixed_vat_rates()
    {
        var vm = new SalesInvoiceFormViewModel
        {
            Items = new()
            {
                new() { ProductId = 1, Quantity = 1, UnitPrice = 100m, VatRate = 15m },
                new() { ProductId = 2, Quantity = 2, UnitPrice = 50m,  VatRate = 0m  }, // zero-rated
            }
        };
        _svc.CalculateSales(vm);

        vm.Subtotal.Should().Be(200m);
        vm.VatTotal.Should().Be(15m); // only first line
        vm.GrandTotal.Should().Be(215m);
    }
}
