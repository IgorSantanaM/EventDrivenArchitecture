namespace EDA.Producer.Core;

public class Order
{
    public string OrderId { get; set; }
    
    public string CustomerId { get; set; }
    public bool Completed { get; set; }
}