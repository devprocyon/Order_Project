using Moq;
using Order_Project.Models;
using Order_Project.Services;
using Order_Project.Services.Intefraces;

namespace Order_Project_Tests
{
    public class OrderServiceTests
    {
        private readonly Mock<IInventoryService> _inventoryMock;
        private readonly Mock<IPaymentService> _paymentMock;
        private readonly Mock<INotificationService> _notificationMock;
        private readonly OrderService _service;

        public OrderServiceTests()
        {
            _inventoryMock = new Mock<IInventoryService>();
            _paymentMock = new Mock<IPaymentService>();
            _notificationMock = new Mock<INotificationService>();

            _service = new OrderService(
                _inventoryMock.Object,
                _paymentMock.Object,
                _notificationMock.Object
            );
        }

        /// <summary>
        /// Verifies that a new order is successfully created
        /// when valid product name and quantity are provided.
        /// Ensures inventory, payment and notification steps are executed.
        /// </summary>
        [Fact]
        public void CreateOrder_ShouldCreateNewOrder_WhenValidInput()
        {
            // Arrange
            string product = "Laptop";
            int id = _service.GetOrders().Count() + 1;
            int quantity = 1;

            _inventoryMock.Setup(i => i.CheckStock(product, quantity)).Returns(true);
            _paymentMock
                .Setup(p =>
                    p.ProcessPayment(
                        It.Is<Order>(o => o.Product == product && o.Quantity == quantity)
                    )
                )
                .Returns(true);

            // Act
            Order result = _service.CreateOrder(product, quantity);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(id, result.Id);
            Assert.Equal(product, result.Product);
            Assert.Equal(quantity, result.Quantity);
            Assert.True(result.IsPaid);
            Assert.Contains(result, _service.GetOrders());

            _inventoryMock.Verify(i => i.CheckStock(product, quantity), Times.Once);
            _inventoryMock.Verify(i => i.ReduceStock(product, quantity), Times.Once);
            _paymentMock.Verify(p => p.ProcessPayment(result), Times.Once);
            _notificationMock.Verify(n => n.SendConfirmation(result), Times.Once);
            _inventoryMock.Verify(i => i.IncreaseStock(product, quantity), Times.Never);
        }

        /// <summary>
        /// Ensures that an exception is thrown when invalid input is provided
        /// such as null/empty product name or non-positive quantity.
        /// Confirms that no services are called and no order is created.
        /// </summary>
        [Theory]
        [InlineData(null, 5)]
        [InlineData("", 5)]
        [InlineData("Keyboard", 0)]
        [InlineData("Keyboard", -2)]
        public void CreateOrder_ShouldThrow_WhenInvalidInput(string product, int quantity)
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentException>(() => _service.CreateOrder(product, quantity));
            Assert.Empty(_service.GetOrders());

            _inventoryMock.Verify(i => i.CheckStock(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
            _inventoryMock.Verify(i => i.ReduceStock(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
            _paymentMock.Verify(p => p.ProcessPayment(It.IsAny<Order>()), Times.Never);
            _notificationMock.Verify(n => n.SendConfirmation(It.IsAny<Order>()), Times.Never);
            _inventoryMock.Verify(i => i.IncreaseStock(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        /// <summary>
        /// Validates that an exception is thrown when creating an order
        /// with insufficient inventory stock. Ensures payment and notifications
        /// are not triggered and that stock is not reduced.
        /// </summary>
        [Fact]
        public void CreateOrder_ShouldThrow_WhenInsufficientStock()
        {
            // Arrange
            string product = "Microphone";
            int quantity = 5;

            _inventoryMock.Setup(i => i.CheckStock(product, quantity)).Returns(false);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => _service.CreateOrder(product, quantity));
            Assert.Empty(_service.GetOrders());

            _inventoryMock.Verify(i => i.CheckStock(product, quantity), Times.Once);
            _inventoryMock.Verify(i => i.ReduceStock(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
            _paymentMock.Verify(p => p.ProcessPayment(It.IsAny<Order>()), Times.Never);
            _notificationMock.Verify(n => n.SendConfirmation(It.IsAny<Order>()), Times.Never);
            _inventoryMock.Verify(i => i.IncreaseStock(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        /// <summary>
        /// Ensures that if payment fails after stock has been reduced,
        /// the order creation is aborted, stock is restored,
        /// and no notification is sent.
        /// </summary>
        [Fact]
        public void CreateOrder_ShouldThrow_WhenPaymentFails()
        {
            // Arrange
            string product = "Mouse";
            int quantity = 4;

            _inventoryMock.Setup(i => i.CheckStock(product, quantity)).Returns(true);
            _paymentMock
                .Setup(p =>
                    p.ProcessPayment(
                        It.Is<Order>(o => o.Product == product && o.Quantity == quantity)
                    )
                )
                .Returns(false);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => _service.CreateOrder(product, quantity));
            Assert.Empty(_service.GetOrders());

            _inventoryMock.Verify(i => i.CheckStock(product, quantity), Times.Once);
            _inventoryMock.Verify(i => i.ReduceStock(product, quantity), Times.Once);
            _paymentMock.Verify(
                p =>
                    p.ProcessPayment(
                        It.Is<Order>(o => o.Product == product && o.Quantity == quantity)
                    ),
                Times.Once
            );
            _notificationMock.Verify(n => n.SendConfirmation(It.IsAny<Order>()), Times.Never);
            _inventoryMock.Verify(i => i.IncreaseStock(product, quantity), Times.Once);
        }

        /// <summary>
        /// Verifies that an existing order is successfully updated
        /// when a valid new quantity is provided. Ensures the method
        /// returns true and the quantity is modified.
        /// </summary>
        [Fact]
        public void UpdateOrder_ShouldUpdateOrderAndReturnsTrue_WhenValidInput()
        {
            // Arrange
            _inventoryMock.Setup(i => i.CheckStock(It.IsAny<string>(), It.IsAny<int>())).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(true);

            Order order = _service.CreateOrder("Lamp", 5);
            int newQuantity = 10;

            // Act
            bool result = _service.UpdateOrder(order.Id, newQuantity);

            // Assert
            Assert.True(result);
            Assert.Equal(newQuantity, order.Quantity);
        }

        /// <summary>
        /// Ensures that updating a non-existent order returns false.
        /// Confirms the order remains unchanged.
        /// </summary>
        [Fact]
        public void UpdateOrder_ShouldReturnsFalse_WhenNonExistentOrder()
        {
            // Arrange
            _inventoryMock.Setup(i => i.CheckStock(It.IsAny<string>(), It.IsAny<int>())).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(true);

            int oldQuantity = 5;
            Order order = _service.CreateOrder("Webcam", oldQuantity);

            // Act
            bool result = _service.UpdateOrder(3, 10);

            // Assert
            Assert.False(result);
            Assert.Equal(oldQuantity, order.Quantity);
        }

        /// <summary>
        /// Ensures that the method returns false when a non-positive quantity is provided.
        /// Confirms the order remains unchanged.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void UpdateOrder_ShouldReturnsFalse_WhenInvalidQuantity(int newQuantity)
        {
            // Arrange
            _inventoryMock.Setup(i => i.CheckStock(It.IsAny<string>(), It.IsAny<int>())).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(true);

            int oldQuantity = 5;
            Order order = _service.CreateOrder("Headphone", oldQuantity);

            // Act
            bool result = _service.UpdateOrder(order.Id, newQuantity);

            // Assert
            Assert.False(result);
            Assert.Equal(oldQuantity, order.Quantity);
        }

        /// <summary>
        /// Ensures that an existing order is successfully removed,
        /// the method returns true, and inventory stock is restored.
        /// </summary>
        [Fact]
        public void RemoveOrder_ShouldRemoveOrderAndReturnsTrue_WhenExistingOrder()
        {
            // Arrange
            _inventoryMock.Setup(i => i.CheckStock(It.IsAny<string>(), It.IsAny<int>())).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(true);

            string product = "Speaker";
            int quantity = 6;
            Order order = _service.CreateOrder(product, quantity);

            // Act
            bool result = _service.RemoveOrder(order.Id);

            // Assert
            Assert.True(result);
            Assert.DoesNotContain(order, _service.GetOrders());

            _inventoryMock.Verify(i => i.IncreaseStock(product, quantity), Times.Once);
        }

        /// <summary>
        /// Ensures that attempting to remove a non-existent order returns false.
        /// Confirms that no inventory updates occur and existing orders remain unchanged.
        /// </summary>
        [Fact]
        public void RemoveOrder_ShouldReturnsFalse_WhenNonExistentOrder()
        {
            // Arrange
            _inventoryMock.Setup(i => i.CheckStock(It.IsAny<string>(), It.IsAny<int>())).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(true);

            Order order = _service.CreateOrder("Printer", 3);

            // Act
            bool result = _service.RemoveOrder(5);

            // Assert
            Assert.False(result);
            Assert.Contains(order, _service.GetOrders());

            _inventoryMock.Verify(i => i.IncreaseStock(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }
    }
}
