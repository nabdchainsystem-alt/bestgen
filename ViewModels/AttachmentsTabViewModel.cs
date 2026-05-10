using bestgen.Models;

namespace bestgen.ViewModels;

public sealed class AttachmentsTabViewModel
{
    public AttachmentDocumentType DocType { get; set; }
    public int DocId { get; set; }
    public string ReturnUrl { get; set; } = string.Empty;
    public IReadOnlyList<Attachment> Items { get; set; } = Array.Empty<Attachment>();
}
