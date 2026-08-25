using System.ComponentModel.DataAnnotations;

namespace OrderManagement.Api.DTOs;

public record ProductDto(int Id, string Name, string Sku, decimal Price, int StockQuantity);

public record CreateProductDto(
    [Required, MaxLength(200)] string Name,
    [Required, MaxLength(50)] string Sku,
    [Range(0, double.MaxValue)] decimal Price,
    [Range(0, int.MaxValue)] int StockQuantity);

public record UpdateProductDto(
    [Required, MaxLength(200)] string Name,
    [Range(0, double.MaxValue)] decimal Price,
    [Range(0, int.MaxValue)] int StockQuantity);
