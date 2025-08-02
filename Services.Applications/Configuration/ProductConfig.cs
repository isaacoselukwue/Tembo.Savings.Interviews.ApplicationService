namespace Services.Applications.Configuration;
public class ProductConfig
{
    public int MinAge { get; init; }
    public int? MaxAge { get; init; }
    public Money MinPayment { get; init; }
}