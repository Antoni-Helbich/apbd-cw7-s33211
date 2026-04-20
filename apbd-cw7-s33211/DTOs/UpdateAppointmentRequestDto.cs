using System.ComponentModel.DataAnnotations;

namespace apbd_cw7_s33211.DTOs;

public class UpdateAppointmentRequestDto
{
    [Required]
    public int IdPatient { get; set; }

    [Required]
    public int IdDoctor { get; set; }

    [Required]
    public DateTime AppointmentDate { get; set; }

    [Required]
    [RegularExpression("^(Scheduled|Completed|Cancelled)$", ErrorMessage = "Status must be Scheduled, Completed, or Cancelled.")]
    public string Status { get; set; } = string.Empty;

    [Required]
    [MaxLength(250)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? InternalNotes { get; set; }
}
