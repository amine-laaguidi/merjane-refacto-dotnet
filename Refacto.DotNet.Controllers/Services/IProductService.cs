using Refacto.DotNet.Controllers.Database.Context;
using Refacto.DotNet.Controllers.Entities;

namespace Refacto.DotNet.Controllers.Services
{

    public interface IProductService
    {
        public void NotifyDelay(int leadTime, Product p);

        public void HandleSeasonalProduct(Product p);

        public void HandleExpiredProduct(Product p);
    }
}
