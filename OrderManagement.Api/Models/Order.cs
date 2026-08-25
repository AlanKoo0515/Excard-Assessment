namespace OrderManagement.Api.Models;

public class Order
{
    public int Id { get; set; }

    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
