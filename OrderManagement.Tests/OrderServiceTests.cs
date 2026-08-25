using Microsoft.EntityFrameworkCore;
using OrderManagement.Api.Data;
using OrderManagement.Api.DTOs;
using OrderManagement.Api.Models;
using OrderManagement.Api.Services;
using Xunit;

namespace OrderManagement.Tests;

public class OrderServiceTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task CreateOrderAsync_RejectsEmptyOrder()
    {
        await using var db = CreateDbContext();
        var service = new OrderService(db);

        var result = await service.CreateOrderAsync(new CreateOrderDto(new List<CreateOrderItemDto>()));

        Assert.False(result.Success);
        Assert.Empty(db.Orders);
    }

    [Fact]
    public async Task CreateOrderAsync_RejectsNonPositiveQuantity()
    {
        await using var db = CreateDbContext();
        db.Products.Add(new Product { Name = "Widget", Sku = "W-1", Price = 10m, StockQuantity = 5 });
        await db.SaveChangesAsync();
        var productId = db.Products.Single().Id;

        var service = new OrderService(db);
        var result = await service.CreateOrderAsync(new CreateOrderDto(
            new List<CreateOrderItemDto> { new(productId, 0) }));

        Assert.False(result.Success);
        Assert.Empty(db.Orders);
    }

    [Fact]
    public async Task CreateOrderAsync_RejectsQuantityExceedingStock()
    {
        await using var db = CreateDbContext();
        db.Products.Add(new Product { Name = "Widget", Sku = "W-1", Price = 10m, StockQuantity = 5 });
        await db.SaveChangesAsync();
        var productId = db.Products.Single().Id;

        var service = new OrderService(db);
        var result = await service.CreateOrderAsync(new CreateOrderDto(
            new List<CreateOrderItemDto> { new(productId, 6) }));

        Assert.False(result.Success);
        Assert.Contains("stock", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(db.Orders);

        // Stock is checked, not managed: the product's StockQuantity is untouched
        // even on a rejected order.
        Assert.Equal(5, db.Products.Single().StockQuantity);
    }

    [Fact]
    public async Task CreateOrderAsync_RejectsNonExistentProduct()
    {
        await using var db = CreateDbContext();
        var service = new OrderService(db);

        var result = await service.CreateOrderAsync(new CreateOrderDto(
            new List<CreateOrderItemDto> { new(999, 1) }));

        Assert.False(result.Success);
        Assert.Contains("999", result.ErrorMessage);
        Assert.Empty(db.Orders);
    }

    [Fact]
    public async Task CreateOrderAsync_PersistsOrderWithCorrectTotal_WhenValid()
    {
        await using var db = CreateDbContext();
        db.Products.AddRange(
            new Product { Name = "Widget", Sku = "W-1", Price = 10m, StockQuantity = 5 },
            new Product { Name = "Gadget", Sku = "G-1", Price = 2.5m, StockQuantity = 5 });
        await db.SaveChangesAsync();
        var widgetId = db.Products.Single(p => p.Sku == "W-1").Id;
        var gadgetId = db.Products.Single(p => p.Sku == "G-1").Id;

        var service = new OrderService(db);
        var result = await service.CreateOrderAsync(new CreateOrderDto(new List<CreateOrderItemDto>
        {
            new(widgetId, 2),  // 2 x 10.00 = 20.00
            new(gadgetId, 3)   // 3 x 2.50  =  7.50
        }));

        Assert.True(result.Success);
        // Stock is checked, not managed: even a successful order leaves StockQuantity untouched.
        Assert.Equal(5, db.Products.Single(p => p.Sku == "W-1").StockQuantity);
        Assert.Equal(5, db.Products.Single(p => p.Sku == "G-1").StockQuantity);
        Assert.NotNull(result.Order);
        Assert.Equal(27.5m, result.Order!.TotalAmount);
        Assert.Equal(2, result.Order.Items.Count);
        Assert.Single(db.Orders);
    }

    [Fact]
    public async Task GetOrderHistoryAsync_ReturnsNewestFirst()
    {
        await using var db = CreateDbContext();
        db.Products.Add(new Product { Name = "Widget", Sku = "W-1", Price = 10m, StockQuantity = 5 });
        await db.SaveChangesAsync();
        var productId = db.Products.Single().Id;

        db.Orders.Add(new Order
        {
            OrderDate = new DateTime(2026, 1, 1),
            OrderItems = { new OrderItem { ProductId = productId, Quantity = 1, UnitPrice = 10m } }
        });
        db.Orders.Add(new Order
        {
            OrderDate = new DateTime(2026, 6, 1),
            OrderItems = { new OrderItem { ProductId = productId, Quantity = 1, UnitPrice = 10m } }
        });
        await db.SaveChangesAsync();

        var service = new OrderService(db);
        var history = await service.GetOrderHistoryAsync();

        Assert.Equal(2, history.Count);
        Assert.True(history[0].OrderDate > history[1].OrderDate);
    }
}
