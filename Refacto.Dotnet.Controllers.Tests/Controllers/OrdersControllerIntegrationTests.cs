using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Refacto.DotNet.Controllers.Database.Context;
using Refacto.DotNet.Controllers.Entities;
using Refacto.DotNet.Controllers.Services;
using Refacto.DotNet.Controllers.Enums.ProductType;

namespace Refacto.Dotnet.Controllers.Tests.Controllers
{
    [Collection("Sequential")]
    public class OrdersControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly AppDbContext _context;
        private readonly Mock<INotificationService> _mockNotificationService;

        public OrdersControllerIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _mockNotificationService = new Mock<INotificationService>();

            _factory = factory.WithWebHostBuilder(builder =>
            {
                _ = builder.ConfigureServices(services =>
                {
                    _ = services.AddSingleton(_mockNotificationService.Object);

                    ServiceDescriptor? descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                    if (descriptor != null)
                    {
                        _ = services.Remove(descriptor);
                    }

                    // Add ApplicationDbContext using an in-memory database for testing
                    _ = services.AddDbContext<AppDbContext>(options =>
                    {
                        _ = options.UseInMemoryDatabase($"InMemoryDbForTesting-{GetType()}");
                    });
                    _ = services.AddScoped((_sp) => _mockNotificationService.Object);


                    ServiceProvider sp = services.BuildServiceProvider();
                });
            });

            IServiceScope scope = _factory.Services.CreateScope();
            _context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            _context.Database.EnsureDeleted();
            _context.Database.EnsureCreated();
        }

        // NORMAL
        [Fact]
        public async Task ProcessOrder_NormalProduct_WhenAvailableIsPositive_ShouldDecrementAvailable()
        {
            Product product = new() { LeadTime = 10, Available = 3, Type = ProductType.NORMAL, Name = "USB Cable" };
            await SeedAndProcess(product);

            Product? result = await _context.Products.FindAsync(product.Id);
            Assert.Equal(2, result!.Available);
            _mockNotificationService.Verify(s => s.SendDelayNotification(It.IsAny<int>(), It.IsAny<string>()), Times.Never());
        }

        [Fact]
        public async Task ProcessOrder_NormalProduct_WhenAvailableIsZeroAndLeadTimeIsPositive_ShouldSendDelayNotification()
        {
            Product product = new() { LeadTime = 10, Available = 0, Type = ProductType.NORMAL, Name = "USB Dongle" };
            await SeedAndProcess(product);

            Product? result = await _context.Products.FindAsync(product.Id);
            Assert.Equal(0, result!.Available);
            _mockNotificationService.Verify(s => s.SendDelayNotification(product.LeadTime, product.Name), Times.Once());
        }

        [Fact]
        public async Task ProcessOrder_NormalProduct_WhenAvailableIsZeroAndLeadTimeIsZero_ShouldNotSendDelayNotification()
        {
            Product product = new() { LeadTime = 0, Available = 0, Type = ProductType.NORMAL, Name = "USB Dongle" };
            await SeedAndProcess(product);

            _mockNotificationService.Verify(s => s.SendDelayNotification(It.IsAny<int>(), It.IsAny<string>()), Times.Never());
        }

        // SEASONAL
        [Fact]
        public async Task ProcessOrder_SeasonalProduct_WhenInSeasonAndAvailableIsPositive_ShouldDecrementAvailable()
        {
            Product product = new() { LeadTime = 5, Available = 3, Type = ProductType.SEASONAL, Name = "Watermelon", SeasonStartDate = DateTime.Now.AddDays(-10), SeasonEndDate = DateTime.Now.AddDays(30) };
            await SeedAndProcess(product);

            Product? result = await _context.Products.FindAsync(product.Id);

            Assert.Equal(2, result!.Available);
            _mockNotificationService.Verify(s => s.SendOutOfStockNotification(It.IsAny<string>()), Times.Never());
        }

        [Fact]
        public async Task ProcessOrder_SeasonalProduct_WhenOutOfSeason_ShouldSetAvailableToZeroAndSendOutOfStockNotification()
        {
            Product product = new() { LeadTime = 5, Available = 3, Type = ProductType.SEASONAL, Name = "Grapes", SeasonStartDate = DateTime.Now.AddDays(10), SeasonEndDate = DateTime.Now.AddDays(90) };
            await SeedAndProcess(product);

            Product? result = await _context.Products.FindAsync(product.Id);

            Assert.Equal(0, result!.Available);
            _mockNotificationService.Verify(s => s.SendOutOfStockNotification(product.Name), Times.Once());
        }

        [Fact]
        public async Task ProcessOrder_SeasonalProduct_WhenLeadTimeExceedsSeason_ShouldSetAvailableToZeroAndSendOutOfStockNotification()
        {
            Product product = new() { LeadTime = 20, Available = 0, Type = ProductType.SEASONAL, Name = "Strawberry", SeasonStartDate = DateTime.Now.AddDays(-10), SeasonEndDate = DateTime.Now.AddDays(10) };
            await SeedAndProcess(product);

            Product? result = await _context.Products.FindAsync(product.Id);

            Assert.Equal(0, result!.Available);
            _mockNotificationService.Verify(s => s.SendOutOfStockNotification(product.Name), Times.Once());
        }

        [Fact]
        public async Task ProcessOrder_SeasonalProduct_WhenAvailableIsPositiveAndInSeason_ShouldDecrementAvailable()
        {
            Product product = new() { LeadTime = 5, Available = 1, Type = ProductType.SEASONAL, Name = "Strawberry", 
                SeasonStartDate = DateTime.Now.AddDays(-10), SeasonEndDate = DateTime.Now.AddDays(30) };
            await SeedAndProcess(product);

            Product? result = await _context.Products.FindAsync(product.Id);
            Assert.Equal(0, result!.Available);
            _mockNotificationService.Verify(s => s.SendDelayNotification(product.LeadTime, product.Name), Times.Never());
        }

        [Fact]
        public async Task ProcessOrder_ExpirableProduct_WhenAvailableIsPositiveAndExpiryDateIsInTheFuture_ShouldDecrementAvailable()
        {
            Product product = new() { LeadTime = 5, Available = 3, Type = ProductType.EXPIRABLE, Name = "Milk", ExpiryDate = DateTime.Now.AddDays(10) };
            await SeedAndProcess(product);

            Product? result = await _context.Products.FindAsync(product.Id);
            Assert.Equal(2, result!.Available);
            _mockNotificationService.Verify(s => s.SendExpirationNotification(product.Name, product.ExpiryDate.Value), Times.Never());
        }

        [Fact]
        public async Task ProcessOrder_ExpirableProduct_WhenAvailableIsPositiveAndExpiryDateIsInThePast_ShouldSetAvailableToZeroAndSendExpirationNotification()
        {
            Product product = new() { LeadTime = 5, Available = 3, Type = ProductType.EXPIRABLE, Name = "Milk", ExpiryDate = DateTime.Now.AddDays(-10) };
            await SeedAndProcess(product);

            Product? result = await _context.Products.FindAsync(product.Id);

            Assert.Equal(0, result!.Available);
            _mockNotificationService.Verify(s => s.SendExpirationNotification(product.Name, product.ExpiryDate.Value), Times.Once());
        }

        [Fact]
        public async Task ProcessOrder_ExpirableProduct_WhenAvailableIsZeroAndExpiryDateIsInTheFuture_ShouldNotSendExpirationNotification()
        {
            Product product = new() { LeadTime = 5, Available = 0, Type = ProductType.EXPIRABLE, Name = "Milk", ExpiryDate = DateTime.Now.AddDays(10) };
            await SeedAndProcess(product);

            Product? result = await _context.Products.FindAsync(product.Id);

            Assert.Equal(0, result!.Available);
            _mockNotificationService.Verify(s => s.SendExpirationNotification(product.Name, product.ExpiryDate.Value), Times.Never());
        }

        [Fact]
        public async Task ProcessOrder_ExpirableProduct_WhenAvailableIsZeroAndExpiryDateIsInThePast_ShouldSetAvailableToZeroAndSendExpirationNotification()
        {
            Product product = new() { LeadTime = 5, Available = 0, Type = ProductType.EXPIRABLE, Name = "Milk", ExpiryDate = DateTime.Now.AddDays(-10) };
            await SeedAndProcess(product);

            Product? result = await _context.Products.FindAsync(product.Id);

            Assert.Equal(0, result!.Available);
            _mockNotificationService.Verify(s => s.SendExpirationNotification(product.Name, product.ExpiryDate.Value), Times.Once());
        }

        [Fact]
        public async Task ProcessOrder_ExpirableProduct_WhenNotAvailableAndNotExpired_ShouldNotSaveAndShouldSendDelayNotification()
        {
            Product product = new() { LeadTime = 5, Available = 0, Type = ProductType.EXPIRABLE, Name = "Milk", ExpiryDate = DateTime.Now.AddDays(10) };
            await SeedAndProcess(product);

            Product? result = await _context.Products.FindAsync(product.Id);

            Assert.Equal(0, result!.Available);
            _mockNotificationService.Verify(s => s.SendExpirationNotification(product.Name, product.ExpiryDate.Value), Times.Never());
        }

        private async Task SeedAndProcess(Product product)
        {
            HttpClient client = _factory.CreateClient();
            Order order = new() { Items = new HashSet<Product> { product } };
            await _context.Products.AddAsync(product);
            _ = await _context.Orders.AddAsync(order);
            _ = await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            HttpResponseMessage response = await client.PostAsync($"/orders/{order.Id}/processOrder", null);
            _ = response.EnsureSuccessStatusCode();
        }
    }
}
