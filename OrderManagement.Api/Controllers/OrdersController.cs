using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderManagement.Api.Data;
using OrderManagement.Api.DTOs;
using OrderManagement.Api.Models;

namespace OrderManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _db;

    public OrdersController(AppDbContext db) => _db = db;

    private static OrderDto ToDto(Order o) => new(
        o.Id,
        o.CustomerId,
        o.Customer?.Name ?? string.Empty,
        o.OrderDate,
        o.Status,
        o.OrderItems.Sum(i => i.Quantity * i.UnitPrice),
        o.OrderItems.Select(i => new OrderItemDto(i.Id, i.ProductId, i.Product?.Name ?? string.Empty, i.Quantity, i.UnitPrice)).ToList());

    private IQueryable<Order> OrdersWithDetails() =>
        _db.Orders.Include(o => o.Customer).Include(o => o.OrderItems).ThenInclude(i => i.Product);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderDto>>> GetAll()
    {
        var orders = await OrdersWithDetails().AsNoTracking().ToListAsync();
        return Ok(orders.Select(ToDto));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderDto>> GetById(int id)
    {
        var order = await OrdersWithDetails().AsNoTracking().FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return NotFound();
        return Ok(ToDto(order));
    }

    [HttpPost]
    public async Task<ActionResult<OrderDto>> Create(CreateOrderDto dto)
    {
        var customer = await _db.Customers.FindAsync(dto.CustomerId);
        if (customer is null) return BadRequest($"Customer {dto.CustomerId} does not exist.");

        var productIds = dto.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _db.Products.Where(p => productIds.Contains(p.Id)).ToListAsync();

        if (products.Count != productIds.Count)
            return BadRequest("One or more products do not exist.");

        var order = new Order
        {
            CustomerId = dto.CustomerId,
            Status = OrderStatus.Pending,
            OrderDate = DateTime.UtcNow
        };

        foreach (var item in dto.Items)
        {
            var product = products.First(p => p.Id == item.ProductId);
            if (product.StockQuantity < item.Quantity)
                return BadRequest($"Insufficient stock for product '{product.Name}'. Available: {product.StockQuantity}, requested: {item.Quantity}.");

            product.StockQuantity -= item.Quantity;

            order.OrderItems.Add(new OrderItem
            {
                ProductId = product.Id,
                Quantity = item.Quantity,
                UnitPrice = product.Price
            });
        }

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        var created = await OrdersWithDetails().AsNoTracking().FirstAsync(o => o.Id == order.Id);
        return CreatedAtAction(nameof(GetById), new { id = order.Id }, ToDto(created));
    }

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, UpdateOrderStatusDto dto)
    {
        var order = await _db.Orders.FindAsync(id);
        if (order is null) return NotFound();

        order.Status = dto.Status;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var order = await _db.Orders.Include(o => o.OrderItems).FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return NotFound();

        if (order.Status is not (OrderStatus.Pending or OrderStatus.Cancelled))
            return Conflict("Only pending or cancelled orders can be deleted.");

        if (order.Status == OrderStatus.Pending)
        {
            var products = await _db.Products.Where(p => order.OrderItems.Select(i => i.ProductId).Contains(p.Id)).ToListAsync();
            foreach (var item in order.OrderItems)
            {
                var product = products.First(p => p.Id == item.ProductId);
                product.StockQuantity += item.Quantity;
            }
        }

        _db.Orders.Remove(order);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
