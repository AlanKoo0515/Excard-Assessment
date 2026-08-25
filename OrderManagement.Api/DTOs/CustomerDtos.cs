using System.ComponentModel.DataAnnotations;

namespace OrderManagement.Api.DTOs;

public record CustomerDto(int Id, string Name, string Email, string? Phone, string? Address);

public record CreateCustomerDto(
    [Required, MaxLength(200)] string Name,
    [Required, EmailAddress, MaxLength(200)] string Email,
    [MaxLength(30)] string? Phone,
    [MaxLength(400)] string? Address);

public record UpdateCustomerDto(
    [Required, MaxLength(200)] string Name,
    [MaxLength(30)] string? Phone,
    [MaxLength(400)] string? Address);
