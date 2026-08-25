using System.ComponentModel.DataAnnotations;

namespace OrderManagement.Api.Models;

public class Customer
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? Phone { get; set; }

    [MaxLength(400)]
    public string? Address { get; set; }

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
