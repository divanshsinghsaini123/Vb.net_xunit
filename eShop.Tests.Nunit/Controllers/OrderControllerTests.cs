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
using NUnit.Framework;

namespace _nunittests.Controllers
{
    [TestFixture]
    public class OrderControllerTests
    {
        private const string TestUserName = "testuser@example.com";
        private Mock<IOrderRepository> _mockOrderRepository;
        private OrderController _controller;

        [SetUp]
        public void SetUp()
        {
            _mockOrderRepository = new Mock<IOrderRepository>(MockBehavior.Strict);
            _controller = new OrderController(_mockOrderRepository.Object);
            SetupControllerContext();
        }

        private void SetupControllerContext()
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, TestUserName)
            }, "mock"));

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
        }

        #region MyOrders Action Tests

        [Test]
        public async Task MyOrders_WhenCalled_ReturnsViewWithOrderViewModels()
        {
            var testOrders = GetTestOrders();
            _mockOrderRepository.Setup(x => x.ListAsync(It.IsAny<CustomerOrdersWithItemsSpecification>()))
                .ReturnsAsync(testOrders);

            var result = await _controller.MyOrders();

            var viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);

            var model = viewResult.Model as IEnumerable<OrderViewModel>;
            Assert.IsNotNull(model);
            Assert.That(model.Count(), Is.EqualTo(testOrders.Count));
            _mockOrderRepository.Verify(x => x.ListAsync(It.IsAny<CustomerOrdersWithItemsSpecification>()), Times.Once);
        }

        [Test]
        public async Task MyOrders_WhenNoOrdersExist_ReturnsEmptyViewModel()
        {
            _mockOrderRepository.Setup(x => x.ListAsync(It.IsAny<CustomerOrdersWithItemsSpecification>()))
                .ReturnsAsync(new List<Order>());

            var result = await _controller.MyOrders();

            var viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);

            var model = viewResult.Model as IEnumerable<OrderViewModel>;
            Assert.IsNotNull(model);
            Assert.That(model, Is.Empty);
            _mockOrderRepository.Verify(x => x.ListAsync(It.IsAny<CustomerOrdersWithItemsSpecification>()), Times.Once);
        }

        [Test]
        public void MyOrders_WhenRepositoryThrowsException_PropagatesException()
        {
            _mockOrderRepository.Setup(x => x.ListAsync(It.IsAny<CustomerOrdersWithItemsSpecification>()))
                .ThrowsAsync(new ApplicationException("Database error"));

            Assert.ThrowsAsync<ApplicationException>(() => _controller.MyOrders());
            _mockOrderRepository.Verify(x => x.ListAsync(It.IsAny<CustomerOrdersWithItemsSpecification>()), Times.Once);
        }

        #endregion

        #region Detail Action Tests

        [Test]
        public async Task Detail_WithValidOrderId_ReturnsViewWithOrderViewModel()
        {
            var testOrder = GetTestOrders().First();
            var customerOrders = GetTestOrders();
            _mockOrderRepository.Setup(x => x.ListAsync(It.IsAny<CustomerOrdersWithItemsSpecification>()))
                .ReturnsAsync(customerOrders);

            var result = await _controller.Detail(testOrder.Id);

            var viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);

            var model = viewResult.Model as OrderViewModel;
            Assert.IsNotNull(model);
            Assert.That(model.OrderNumber, Is.EqualTo(testOrder.Id));
            Assert.That(model.Total, Is.EqualTo(testOrder.Total()));
            Assert.That(model.Status, Is.EqualTo("Pending"));
            _mockOrderRepository.Verify(x => x.ListAsync(It.IsAny<CustomerOrdersWithItemsSpecification>()), Times.Once);
        }

        [Test]
        public async Task Detail_WithInvalidOrderId_ReturnsBadRequest()
        {
            var invalidOrderId = 999;
            var customerOrders = GetTestOrders();
            _mockOrderRepository.Setup(x => x.ListAsync(It.IsAny<CustomerOrdersWithItemsSpecification>()))
                .ReturnsAsync(customerOrders);

            var result = await _controller.Detail(invalidOrderId);

            var badRequest = result as BadRequestObjectResult;
            Assert.IsNotNull(badRequest);
            Assert.That(badRequest.Value, Is.EqualTo("No such order found for this user."));
            _mockOrderRepository.Verify(x => x.ListAsync(It.IsAny<CustomerOrdersWithItemsSpecification>()), Times.Once);
        }

        [Test]
        public async Task Detail_WhenOrderBelongsToDifferentUser_ReturnsBadRequest()
        {
            var otherUserOrder = new Order("-1", new Address("1", "2", "3", "4", "5"));
            var customerOrders = new List<Order> { otherUserOrder };
            _mockOrderRepository.Setup(x => x.ListAsync(It.IsAny<CustomerOrdersWithItemsSpecification>()))
                .ReturnsAsync(customerOrders);

            var result = await _controller.Detail(1);

            var badRequest = result as BadRequestObjectResult;
            Assert.IsNotNull(badRequest);
            Assert.That(badRequest.Value, Is.EqualTo("No such order found for this user."));
            _mockOrderRepository.Verify(x => x.ListAsync(It.IsAny<CustomerOrdersWithItemsSpecification>()), Times.Once);
        }

        [Test]
        public void Detail_WhenRepositoryThrowsException_PropagatesException()
        {
            _mockOrderRepository.Setup(x => x.ListAsync(It.IsAny<CustomerOrdersWithItemsSpecification>()))
                .ThrowsAsync(new ApplicationException("Database error"));

            Assert.ThrowsAsync<ApplicationException>(() => _controller.Detail(1));
            _mockOrderRepository.Verify(x => x.ListAsync(It.IsAny<CustomerOrdersWithItemsSpecification>()), Times.Once);
        }

        #endregion

        #region Test Data Builder

        private List<Order> GetTestOrders()
        {
            var address = new Address("Street", "City", "State", "Country", "ZipCode");
            return new List<Order>
            {
                new Order(TestUserName, address, new List<OrderItem>
                {
                    new OrderItem(new CatalogItemOrdered(1, "Product1", "test1.jpg"), 10.50m, 2),
                    new OrderItem(new CatalogItemOrdered(2, "Product2", "test2.jpg"), 15.25m, 1)
                })
                {
                    Id = 1,
                    OrderDate = DateTimeOffset.Now
                },
                new Order(TestUserName, address, new List<OrderItem>
                {
                    new OrderItem(new CatalogItemOrdered(3, "Product3", "test3.jpg"), 20.00m, 3)
                })
                {
                    Id = 2,
                    OrderDate = DateTimeOffset.Now.AddDays(-1)
                }
            };
        }

        #endregion
    }
}
