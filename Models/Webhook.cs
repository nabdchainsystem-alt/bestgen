using System.ComponentModel.DataAnnotations;

namespace bestgen.Models;

public class Webhook
{
    public int Id { get; set; }

    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string Url { get; set; } = string.Empty;

    /// <summary>Comma-separated event names: invoice.created, invoice.paid, customer.created, ...</summary>
    [Required, StringLength(500)]
    public string Events { get; set; } = "*";

    /// <summary>Secret used to HMAC-sign the payload. Sent as X-Bestgen-Signature header.</summary>
    [StringLength(120)]
    public string? Secret { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastDeliveredAt { get; set; }
    public int FailureCount { get; set; }
}

public enum WebhookDeliveryStatus { Queued, Delivered, Failed }

public class WebhookDelivery
{
    public int Id { get; set; }
    public int WebhookId { get; set; }

    [Required, StringLength(64)]
    public string Event { get; set; } = string.Empty;

    [Required]
    public string Payload { get; set; } = string.Empty;

    public WebhookDeliveryStatus Status { get; set; } = WebhookDeliveryStatus.Queued;
    public int? ResponseStatus { get; set; }

    [StringLength(2000)]
    public string? ResponseBody { get; set; }

    [StringLength(500)]
    public string? Error { get; set; }

    public int Attempts { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeliveredAt { get; set; }

    public Webhook? Webhook { get; set; }
}
