Imports System.Security.Claims
Imports Microsoft.AspNetCore.Http
Imports Microsoft.AspNetCore.Mvc
Imports Microsoft.eShopWeb.ApplicationCore.Entities
Imports Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate
Imports Microsoft.eShopWeb.ApplicationCore.Interfaces
Imports Microsoft.eShopWeb.ApplicationCore.Specifications
Imports Microsoft.eShopWeb.Web.Controllers
Imports Moq
Imports Xunit

Public Class OrderControllerTests

    Private ReadOnly _mockRepo As Mock(Of IOrderRepository)
    Private ReadOnly _controller As OrderController

    Public Sub New()
        _mockRepo = New Mock(Of IOrderRepository)()

        ' Set up mock user identity
        _controller = New OrderController(_mockRepo.Object)
        Dim fakeUser = New ClaimsPrincipal(New ClaimsIdentity({New Claim(ClaimTypes.Name, "testuser@example.com")}, "mock"))
        _controller.ControllerContext = New ControllerContext() With {
            .HttpContext = New DefaultHttpContext() With {
                .User = fakeUser
            }
        }
    End Sub

    <Fact>
    Public Async Function MyOrders_ReturnsOrdersView() As Task
        ' Arrange
        Dim order = CreateFakeOrder(1)
        _mockRepo.Setup(Function(r) r.ListAsync(It.IsAny(Of CustomerOrdersWithItemsSpecification))) _
                 .ReturnsAsync(New List(Of Order) From {order})

        ' Act
        Dim result = Await _controller.MyOrders()

        ' Assert
        Dim view = Assert.IsType(Of ViewResult)(result)
        Assert.NotNull(view.Model)
    End Function

    <Fact>
    Public Async Function Detail_ReturnsCorrectOrderView() As Task
        ' Arrange
        Dim order = CreateFakeOrder(5)
        _mockRepo.Setup(Function(r) r.ListAsync(It.IsAny(Of CustomerOrdersWithItemsSpecification))) _
                 .ReturnsAsync(New List(Of Order) From {order})

        ' Act
        Dim result = Await _controller.Detail(5)

        ' Assert
        Dim view = Assert.IsType(Of ViewResult)(result)
        Dim model = view.Model
        Assert.NotNull(model)
    End Function

    <Fact>
    Public Async Function Detail_ReturnsBadRequest_WhenOrderNotFound() As Task
        '   Arrange
        _mockRepo.Setup(Function(r) r.ListAsync(It.IsAny(Of CustomerOrdersWithItemsSpecification))) _
                 .ReturnsAsync(New List(Of Order)()) ' No order found

        ' Act
        Dim result = Await _controller.Detail(99)

        ' Assert
        Dim badRequest = Assert.IsType(Of BadRequestObjectResult)(result)
        Assert.Equal("No such order found for this user.", badRequest.Value)
    End Function


    Private Function CreateFakeOrder(id As Integer) As Order
        Dim items = New List(Of OrderItem) From {
            New OrderItem(New CatalogItemOrdered(1, "Test Product", "img.jpg"), 10D, 2)
        }
        Dim order = New Order("testuser@example.com", New Address("A", "B", "C", "D", "00000"), items)
        ' If you must set the ID for assertions, use reflection:
        Dim idProp = GetType(Order).BaseType.GetProperty("Id")
        idProp.SetValue(order, id)
        Return order
    End Function

End Class
