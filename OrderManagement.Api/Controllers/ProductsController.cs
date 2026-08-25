using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderManagement.Api.Data;
using OrderManagement.Api.DTOs;

namespace OrderManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProductsController(AppDbContext db) => _db = db;

    private static ProductDto ToDto(Models.Product p) => new(p.Id, p.Name, p.Sku, p.Description, p.Price, p.StockQuantity);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetAll()
    {
        var products = await _db.Products.AsNoTracking().OrderBy(p => p.Name).ToListAsync();
        return Ok(products.Select(ToDto));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductDto>> GetById(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product is null) return NotFound();
        return Ok(ToDto(product));
    }
}
