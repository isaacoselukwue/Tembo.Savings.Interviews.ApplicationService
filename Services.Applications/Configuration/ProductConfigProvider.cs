namespace Services.Applications.Configuration;
public class ProductConfigProvider : IProductConfigProvider
{
    private readonly IReadOnlyDictionary<ProductCode, ProductConfig> _configs = new Dictionary<ProductCode, ProductConfig>
    {
        [ProductCode.ProductOne] = new()
        {
            MinAge = 18,
            MaxAge = 39,
            MinPayment = new("GBP", 0.99m)
        },
        [ProductCode.ProductTwo] = new()
        {
            MinAge = 18,
            MaxAge = null,
            MinPayment = new("GBP", 0.99m)
        }
    };

    public ProductConfig GetConfigFor(ProductCode productCode)
    {
        if (_configs.TryGetValue(productCode, out ProductConfig? config))
        {
            return config;
        }
        throw new KeyNotFoundException($"No product configuration found for {productCode}");
    }
}