using Refacto.DotNet.Controllers.Database.Context;
using Refacto.DotNet.Controllers.Entities;

namespace Refacto.DotNet.Controllers.Services
{

    public interface IProductService
    {

        public void HandleSeasonalProduct(Product p);

        public void HandleExpiredProduct(Product p);

        public void HandleNormalProduct(Product p);
    }
}
