using Refacto.DotNet.Controllers.Database.Context;
using Refacto.DotNet.Controllers.Entities;

namespace Refacto.DotNet.Controllers.Services
{

    public interface IOrderService
    {
        public void ProcessOrder(long orderId);
    }
}
