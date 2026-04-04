using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PaymentService.Models;

public class Payment
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    [Column(TypeName = "numeric(18,2)")]
    public decimal Amount { get; set; }

    [MaxLength(100)]
    public string? Method { get; set; }

    [MaxLength(100)]
    public string TransactionId { get; set; } = string.Empty;

    public bool Succeeded { get; set; }

    [MaxLength(500)]
    public string? ErrorMessage { get; set; }

    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}
