using Microsoft.EntityFrameworkCore;
using Moq;
using Moq.EntityFrameworkCore;
using Refacto.DotNet.Controllers.Database.Context;
using Refacto.DotNet.Controllers.Entities;
using Refacto.DotNet.Controllers.Enums.ProductType;
using Refacto.DotNet.Controllers.Services;
using Refacto.DotNet.Controllers.Services.Impl;

namespace Refacto.DotNet.Controllers.Tests.Services
{
    public class ProductServiceTests
    {
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly Mock<AppDbContext> _mockDbContext;
        private readonly Mock<DbSet<Product>> _mockDbSet;
        private readonly ProductService _productService;

        public ProductServiceTests()
        {
            _mockNotificationService = new Mock<INotificationService>();
            _mockDbContext = new Mock<AppDbContext>();
            _mockDbSet = new Mock<DbSet<Product>>();
            _ = _mockDbContext.Setup(x => x.Products).ReturnsDbSet(Array.Empty<Product>());
            _productService = new ProductService(_mockNotificationService.Object, _mockDbContext.Object);
        }

        [Fact]
        public void HandleNormalProduct_WhenAvailableIsPositive_ShouldDecrementAvailableAndNotNotify()
        {

            Product product = new()
            {
                LeadTime = 10,
                Available = 3,
                Type = ProductType.NORMAL,
                Name = "RJ45 Cable"
            };


            _productService.HandleNormalProduct(product);


            Assert.Equal(2, product.Available);
            _mockDbContext.Verify(ctx => ctx.SaveChanges(), Times.Once());
            _mockNotificationService.Verify(service => service.SendDelayNotification(It.IsAny<int>(), It.IsAny<string>()), Times.Never());
        }

        [Fact]
        public void HandleNormalProduct_WhenAvailableBecomesZero_ShouldSendDelayNotification()
        {
            
            Product product = new()
            {
                LeadTime = 10,
                Available = 1,
                Type = ProductType.NORMAL,
                Name = "RJ45 Cable"
            };


            _productService.HandleNormalProduct(product);


            Assert.Equal(0, product.Available);
            _mockDbContext.Verify(ctx => ctx.SaveChanges(), Times.Once());
            _mockNotificationService.Verify(service => service.SendDelayNotification(product.LeadTime, product.Name), Times.Once());
        }

        [Fact]
        public void NotifyDelay_WhenProductAvailableIsZero_ShouldSaveAndSendDelayNotification()
        {
            // GIVEN
            Product product = new()
            {
                LeadTime = 15,
                Available = 0,
                Type = ProductType.NORMAL,
                Name = "RJ45 Cable"
            };

            // WHEN
            _productService.NotifyDelay(product.LeadTime, product);

            // THEN
            Assert.Equal(0, product.Available);
            Assert.Equal(15, product.LeadTime);
            _mockDbContext.Verify(ctx => ctx.SaveChanges(), Times.Once());
            _mockNotificationService.Verify(service => service.SendDelayNotification(product.LeadTime, product.Name), Times.Once());
        }

        [Fact]
        public void HandleSeasonalProduct_WhenInSeasonAndAvailableIsPositive_ShouldDecrementAvailableAndNotNotify()
        {
            
            Product product = new()
            {
                LeadTime = 5,
                Available = 3,
                Type = ProductType.SEASONAL,
                Name = "Strawberry",
                SeasonStartDate = DateTime.Now.AddDays(-10),
                SeasonEndDate = DateTime.Now.AddDays(30)
            };

            
            _productService.HandleSeasonalProduct(product);

            
            Assert.Equal(2, product.Available);
            _mockDbContext.Verify(ctx => ctx.SaveChanges(), Times.Once());
            _mockNotificationService.Verify(service => service.SendDelayNotification(It.IsAny<int>(), It.IsAny<string>()), Times.Never());
            _mockNotificationService.Verify(service => service.SendOutOfStockNotification(It.IsAny<string>()), Times.Never());
        }

        [Fact]
        public void HandleSeasonalProduct_WhenBeforeSeasonStart_ShouldSetAvailableToZeroAndSendOutOfStockNotification()
        {
            
            Product product = new()
            {
                LeadTime = 5,
                Available = 3,
                Type = ProductType.SEASONAL,
                Name = "Strawberry",
                SeasonStartDate = DateTime.Now.AddDays(10),
                SeasonEndDate = DateTime.Now.AddDays(90)
            };

            
            _productService.HandleSeasonalProduct(product);

            
            Assert.Equal(0, product.Available);
            _mockNotificationService.Verify(service => service.SendOutOfStockNotification(product.Name), Times.Once());
            _mockNotificationService.Verify(service => service.SendDelayNotification(It.IsAny<int>(), It.IsAny<string>()), Times.Never());
            _mockDbContext.Verify(ctx => ctx.SaveChanges(), Times.Once());
        }
    }
}
