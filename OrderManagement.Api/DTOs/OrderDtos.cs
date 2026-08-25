using System.ComponentModel.DataAnnotations;

namespace OrderManagement.Api.DTOs;

public record OrderItemDto(int Id, int ProductId, string ProductName, int Quantity, decimal UnitPrice)
{
    public decimal LineTotal => Quantity * UnitPrice;
}

public record OrderDto(int Id, DateTime OrderDate, decimal TotalAmount, List<OrderItemDto> Items);

public record CreateOrderItemDto(
    [Required] int ProductId,
    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be a positive whole number.")] int Quantity);

public record CreateOrderDto(
    [Required, MinLength(1, ErrorMessage = "An order must contain at least one item.")]
    List<CreateOrderItemDto> Items);
