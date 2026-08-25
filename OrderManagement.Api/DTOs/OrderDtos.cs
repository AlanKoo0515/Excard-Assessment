using System.ComponentModel.DataAnnotations;
using OrderManagement.Api.Models;

namespace OrderManagement.Api.DTOs;

public record OrderItemDto(int Id, int ProductId, string ProductName, int Quantity, decimal UnitPrice);

public record OrderDto(
    int Id,
    int CustomerId,
    string CustomerName,
    DateTime OrderDate,
    OrderStatus Status,
    decimal TotalAmount,
    List<OrderItemDto> Items);

public record CreateOrderItemDto(
    [Required] int ProductId,
    [Range(1, int.MaxValue)] int Quantity);

public record CreateOrderDto(
    [Required] int CustomerId,
    [MinLength(1)] List<CreateOrderItemDto> Items);

public record UpdateOrderStatusDto([Required] OrderStatus Status);
