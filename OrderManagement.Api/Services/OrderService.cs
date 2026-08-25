using Microsoft.EntityFrameworkCore;
using OrderManagement.Api.Data;
using OrderManagement.Api.DTOs;
using OrderManagement.Api.Models;

namespace OrderManagement.Api.Services;

public record OrderResult(bool Success, string? ErrorMessage, OrderDto? Order)
{
    public static OrderResult Fail(string message) => new(false, message, null);
    public static OrderResult Ok(OrderDto order) => new(true, null, order);
}

public interface IOrderService
{
    Task<OrderResult> CreateOrderAsync(CreateOrderDto request);
    Task<List<OrderDto>> GetOrderHistoryAsync();
}

public class OrderService : IOrderService
{
    private readonly AppDbContext _db;

    public OrderService(AppDbContext db) => _db = db;

    private static OrderDto ToDto(Order o) => new(
        o.Id,
        o.OrderDate,
        o.OrderItems.Sum(i => i.Quantity * i.UnitPrice),
        o.OrderItems
            .Select(i => new OrderItemDto(i.Id, i.ProductId, i.Product?.Name ?? string.Empty, i.Quantity, i.UnitPrice))
            .ToList());

    public async Task<OrderResult> CreateOrderAsync(CreateOrderDto request)
    {
        // Empty order: rejected outright. An order with nothing in it has no meaning to
        // persist, so we fail fast here rather than writing an empty order row.
        if (request.Items is null || request.Items.Count == 0)
            return OrderResult.Fail("An order must contain at least one item.");

        foreach (var item in request.Items)
        {
            if (item.Quantity <= 0)
                return OrderResult.Fail($"Quantity for product {item.ProductId} must be a positive whole number.");
        }

        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var missingIds = productIds.Where(id => !products.ContainsKey(id)).ToList();
        if (missingIds.Count > 0)
            return OrderResult.Fail($"Product(s) not found: {string.Join(", ", missingIds)}.");

        // Stock is a read-only check here, not managed inventory: we validate the
        // requested quantity against the current StockQuantity, but never decrement it.
        var requestedByProduct = request.Items
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

        foreach (var (productId, requestedQuantity) in requestedByProduct)
        {
            var product = products[productId];
            if (requestedQuantity > product.StockQuantity)
                return OrderResult.Fail(
                    $"Requested quantity for '{product.Name}' ({requestedQuantity}) exceeds available stock ({product.StockQuantity}).");
        }

        var order = new Order
        {
            OrderDate = DateTime.UtcNow
        };

        foreach (var item in request.Items)
        {
            var product = products[item.ProductId];
            order.OrderItems.Add(new OrderItem
            {
                ProductId = product.Id,
                Quantity = item.Quantity,
                UnitPrice = product.Price
            });
        }

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        var saved = await _db.Orders
            .Include(o => o.OrderItems)
            .ThenInclude(i => i.Product)
            .AsNoTracking()
            .FirstAsync(o => o.Id == order.Id);

        return OrderResult.Ok(ToDto(saved));
    }

    public async Task<List<OrderDto>> GetOrderHistoryAsync()
    {
        var orders = await _db.Orders
            .Include(o => o.OrderItems)
            .ThenInclude(i => i.Product)
            .AsNoTracking()
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();

        return orders.Select(ToDto).ToList();
    }
}
