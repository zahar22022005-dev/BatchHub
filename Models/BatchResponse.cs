namespace BatchHub.Models;

public class BatchResponse
{
    public int Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int OriginalQuantity { get; set; }
    public int AdjustedQuantity { get; set; }
    public decimal Price { get; set; }
    public decimal TotalCost => AdjustedQuantity * Price;
    public double DemandFactor { get; set; }
}