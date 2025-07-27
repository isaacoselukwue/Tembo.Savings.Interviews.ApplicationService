namespace Services.Applications.Configuration;
public interface IProductConfigProvider
{
    ProductConfig GetConfigFor(ProductCode productCode);
}