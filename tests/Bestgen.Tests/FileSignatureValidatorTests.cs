using bestgen.Services.Attachments;
using FluentAssertions;
using Xunit;

namespace Bestgen.Tests;

public class FileSignatureValidatorTests
{
    [Fact]
    public void Pdf_starts_with_percent_PDF()
    {
        using var stream = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D });
        FileSignatureValidator.Validate(stream, ".pdf").Should().BeTrue();
    }

    [Fact]
    public void Renamed_exe_to_pdf_is_rejected()
    {
        using var stream = new MemoryStream(new byte[] { 0x4D, 0x5A, 0x90, 0x00 }); // PE/EXE header "MZ"
        FileSignatureValidator.Validate(stream, ".pdf").Should().BeFalse();
    }

    [Fact]
    public void Png_signature_recognized()
    {
        using var stream = new MemoryStream(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00 });
        FileSignatureValidator.Validate(stream, ".png").Should().BeTrue();
    }

    [Fact]
    public void Unknown_extension_passes_through()
    {
        using var stream = new MemoryStream(new byte[] { 0xDE, 0xAD });
        // .csv has no signature in our table → allow
        FileSignatureValidator.Validate(stream, ".csv").Should().BeTrue();
    }

    [Fact]
    public void Stream_position_reset_after_validation()
    {
        using var stream = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46, 0xFF, 0xFF, 0xFF });
        FileSignatureValidator.Validate(stream, ".pdf");
        stream.Position.Should().Be(0);
    }
}
