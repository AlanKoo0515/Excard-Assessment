using Microsoft.AspNetCore.Mvc;
using OrderManagement.Api.DTOs;
using OrderManagement.Api.Services;

namespace OrderManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService) => _orderService = orderService;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderDto>>> GetHistory()
    {
        var orders = await _orderService.GetOrderHistoryAsync();
        return Ok(orders);
    }

    [HttpPost]
    public async Task<ActionResult<OrderDto>> Create(CreateOrderDto dto)
    {
        // ModelState covers the shape-level checks (Required, Range on Quantity, etc.).
        // Existence checks (does the product actually exist?) happen in the service,
        // since that requires a database lookup that data annotations can't express.
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _orderService.CreateOrderAsync(dto);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return CreatedAtAction(nameof(GetHistory), result.Order);
    }
}
