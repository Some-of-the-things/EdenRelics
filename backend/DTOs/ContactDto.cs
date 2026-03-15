using System.ComponentModel.DataAnnotations;

namespace Eden_Relics_BE.DTOs;

public record ContactDto(
    [Required, MaxLength(100)] string Name,
    [Required, EmailAddress, MaxLength(200)] string Email,
    [Required, MaxLength(200)] string Subject,
    [Required, MaxLength(2000)] string Message
);
