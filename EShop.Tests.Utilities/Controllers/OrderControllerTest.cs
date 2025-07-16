using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.eShopWeb.Web.Controllers;
using Microsoft.eShopWeb.Web.ViewModels;
using Moq;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.Web.Controllers
{
    public class OrderControllerTests
    {
        private const string TestUserName = "testuser@example.com";
        private readonly Mock<IOrderRepository> _mockOrderRepository = new Mock<IOrderRepository>(MockBehavior.Strict);
        private readonly OrderController _controller;

        public OrderControllerTests()
        {
            _controller = new OrderController(_mockOrderRepository.Object);
            SetupControllerContext();
        }

        private void SetupControllerContext()
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.Name, TestUserName),
            }, "mock"));

            _controller.ControllerContext = new ControllerContext()
            {
                HttpContext = new DefaultHttpContext() { User = user }
            };
        }

        #region MyOrders Action Tests

        [Fact]
        public async Task MyOrders_WhenCalled_ReturnsViewWithOrderViewModels()
        {
            // Arrange
            var testOrders = GetTestOrders();
            _mockOrderRepository.Setup(x => x.ListAsync(It.IsAny<CustomerOrdersWithItemsSpecification>()))
                .ReturnsAsync(testOrders);

            // Act
            var result = await _controller.MyOrders();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<OrderViewModel>>(viewResult.Model);
            Assert.Equal(testOrders.Count, model.Count());
            _mockOrderRepository.Verify(x => x.ListAsync(It.IsAny<CustomerOrdersWithItemsSpecification>()), Times.Once);
        }

        [Fact]
        public async Task MyOrders_WhenNoOrdersExist_ReturnsEmptyViewModel()
        {
            // Arrange
            _mockOrderRepository.Setup(x => x.ListAsync(It.IsAny<CustomerOrdersWithItemsSpecification>()))
                .ReturnsAsync(new List<Order>());

            // Act
            var result = await _controller.MyOrders();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<OrderViewModel>>(viewResult.Model);
            Assert.Empty(model);
            _mockOrderRepository.Verify(x => x.ListAsync(It.IsAny<CustomerOrdersWithItemsSpecification>()), Times.Once);
        }

        [Fact]
        public async Task MyOrders_WhenRepositoryThrowsException_PropagatesException()
        {
            // Arrange
            _mockOrderRepository.Setup(x => x.ListAsync(It.IsAny<CustomerOrdersWithItemsSpecification>()))
                .ThrowsAsync(new ApplicationException("Database error"));

            // Act & Assert
            await Assert.ThrowsAsync<ApplicationException>(() => _controller.MyOrders());
            _mockOrderRepository.Verify(x => x.ListAsync(It.IsAny<CustomerOrdersWithItemsSpecification>()), Times.Once);
        }

        #endregion

        #region Detail Action Tests

        [Fact]
        public async Task Detail_WithValidOrderId_ReturnsViewWithOrderViewModel()
        {
            // Arrange
            var testOrder = GetTestOrders().First();
            var customerOrders = GetTestOrders();
            _mockOrderRepository.Setup(x => x.ListAsync(It.IsAny<CustomerOrdersWithItemsSpecification>()))
                .ReturnsAsync(customerOrders);

            // Act
            var result = await _controller.Detail(testOrder.Id);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<OrderViewModel>(viewResult.Model);
            Assert.Equal(testOrder.Id, model.OrderNumber);
            Assert.Equal(testOrder.Total(), model.Total);
            Assert.Equal("Pending", model.Status);
            _mockOrderRepository.Verify(x => x.ListAsync(It.IsAny<CustomerOrdersWithItemsSpecification>()), Times.Once);
        }

        [Fact]
        public async Task Detail_WithInvalidOrderId_ReturnsBadRequest()
        {
            // Arrange
            var invalidOrderId = 999;
            var customerOrders = GetTestOrders();
            _mockOrderRepository.Setup(x => x.ListAsync(It.IsAny<CustomerOrdersWithItemsSpecification>()))
                .ReturnsAsync(customerOrders);

            // Act
            var result = await _controller.Detail(invalidOrderId);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("No such order found for this user.", badRequestResult.Value);
            _mockOrderRepository.Verify(x => x.ListAsync(It.IsAny<CustomerOrdersWithItemsSpecification>()), Times.Once);
        }

        [Fact]
        public async Task Detail_WhenOrderBelongsToDifferentUser_ReturnsBadRequest()
        {
            // Arrange
            var otherUserOrder = new Order("-1", new Address("1", "2", "3", "4", "5"));
            var customerOrders = new List<Order> { otherUserOrder };
            _mockOrderRepository.Setup(x => x.ListAsync(It.IsAny<CustomerOrdersWithItemsSpecification>()))
                .ReturnsAsync(customerOrders);

            // Act
            var result = await _controller.Detail(1);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("No such order found for this user.", badRequestResult.Value);
            _mockOrderRepository.Verify(x => x.ListAsync(It.IsAny<CustomerOrdersWithItemsSpecification>()), Times.Once);
        }

        [Fact]
        public async Task Detail_WhenRepositoryThrowsException_PropagatesException()
        {
            // Arrange
            _mockOrderRepository.Setup(x => x.ListAsync(It.IsAny<CustomerOrdersWithItemsSpecification>()))
                .ThrowsAsync(new ApplicationException("Database error"));

            // Act & Assert
            await Assert.ThrowsAsync<ApplicationException>(() => _controller.Detail(1));
            _mockOrderRepository.Verify(x => x.ListAsync(It.IsAny<CustomerOrdersWithItemsSpecification>()), Times.Once);
        }

        #endregion

        #region Test Data Builder

        private List<Order> GetTestOrders()
        {
            var address = new Address("Street", "City", "State", "Country", "ZipCode");
            var orders = new List<Order>
    {
            new Order(
                TestUserName,
                address,
                new List<OrderItem>
                {
                    new OrderItem(new CatalogItemOrdered(1, "Product1", "test1.jpg"), 10.50m, 2),
                    new OrderItem(new CatalogItemOrdered(2, "Product2", "test2.jpg"), 15.25m, 1)
                })
            {
                Id = 1,
                OrderDate = DateTimeOffset.Now
            },
            new Order(
                TestUserName,
                address,
                new List<OrderItem>
                {
                    new OrderItem(new CatalogItemOrdered(3, "Product3", "test3.jpg"), 20.00m, 3)
                })
            {
                Id = 2,
                OrderDate = DateTimeOffset.Now.AddDays(-1)
            }
    };

            return orders;
        }

        #endregion
    }
}