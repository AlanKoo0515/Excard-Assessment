namespace OrderManagement.Api.DTOs;

public record ProductDto(int Id, string Name, string Sku, string? Description, decimal Price, int StockQuantity);
