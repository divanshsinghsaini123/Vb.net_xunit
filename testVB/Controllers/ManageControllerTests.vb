Imports System.Security.Claims
Imports System.Text.Encodings.Web
Imports Microsoft.AspNetCore.Http
Imports Microsoft.AspNetCore.Identity
Imports Microsoft.AspNetCore.Mvc
Imports Microsoft.AspNetCore.Mvc.Routing
Imports Microsoft.eShopWeb.ApplicationCore.Interfaces
Imports Microsoft.eShopWeb.Infrastructure.Identity
Imports Microsoft.eShopWeb.Web.Controllers
Imports Microsoft.eShopWeb.Web.ViewModels.Manage
Imports Moq
Imports Xunit

Public Class ManageControllerTests

    Private ReadOnly _userManager As Mock(Of UserManager(Of ApplicationUser))
    Private ReadOnly _signInManager As Mock(Of SignInManager(Of ApplicationUser))
    Private ReadOnly _emailSender As Mock(Of IEmailSender)
    Private ReadOnly _logger As Mock(Of IAppLogger(Of ManageController))
    Private ReadOnly _urlEncoder As Mock(Of UrlEncoder)
    Private ReadOnly _controller As ManageController

    Public Sub New()
        _userManager = MockUserManager()
        _signInManager = MockSignInManager(_userManager.Object)
        _emailSender = New Mock(Of IEmailSender)()
        _logger = New Mock(Of IAppLogger(Of ManageController))()
        _urlEncoder = New Mock(Of UrlEncoder)()

        _controller = New ManageController(_userManager.Object, _signInManager.Object, _emailSender.Object, _logger.Object, _urlEncoder.Object)

        ' Simulate a logged-in user
        Dim fakeUser = New ClaimsPrincipal(New ClaimsIdentity({New Claim(ClaimTypes.NameIdentifier, "test-user-id")}))
        _controller.ControllerContext = New ControllerContext With {
            .HttpContext = New DefaultHttpContext With {.User = fakeUser}
        }
    End Sub

    <Fact>
    Public Async Function MyAccount_ReturnsView_WhenUserExists() As Task
        ' Arrange
        Dim user = New ApplicationUser With {.UserName = "test", .Email = "test@test.com", .PhoneNumber = "123", .EmailConfirmed = True}
        _userManager.Setup(Function(u) u.GetUserAsync(It.IsAny(Of ClaimsPrincipal))).ReturnsAsync(user)

        ' Act
        Dim result = Await _controller.MyAccount()

        ' Assert
        Dim viewResult = Assert.IsType(Of ViewResult)(result)
        Dim model = Assert.IsType(Of IndexViewModel)(viewResult.Model)
        Assert.Equal("test@test.com", model.Email)
    End Function

    <Fact>
    Public Async Function MyAccount_ThrowsException_WhenUserIsNull() As Task
        _userManager.Setup(Function(u) u.GetUserAsync(It.IsAny(Of ClaimsPrincipal))).ReturnsAsync(CType(Nothing, ApplicationUser))

        Await Assert.ThrowsAsync(Of ApplicationException)(Async Function()
                                                              Return Await _controller.MyAccount()
                                                          End Function)
    End Function

    <Fact>
    Public Async Function Index_ReturnsView_WhenModelStateIsInvalid() As Task
        _controller.ModelState.AddModelError("Email", "Required")
        Dim model = New IndexViewModel With {.Email = "invalid", .PhoneNumber = "123"}

        Dim result = Await _controller.Index(model)

        Dim viewResult = Assert.IsType(Of ViewResult)(result)
        Assert.Equal(model, viewResult.Model)
    End Function

    <Fact>
    Public Async Function Index_UpdatesAndRedirects_WhenValid() As Task
        ' Arrange
        Dim user = New ApplicationUser With {.Id = "123", .Email = "old@test.com", .PhoneNumber = "123"}
        _userManager.Setup(Function(u) u.GetUserAsync(It.IsAny(Of ClaimsPrincipal))).ReturnsAsync(user)
        _userManager.Setup(Function(u) u.SetEmailAsync(user, "new@test.com")).ReturnsAsync(IdentityResult.Success)
        _userManager.Setup(Function(u) u.SetPhoneNumberAsync(user, "999")).ReturnsAsync(IdentityResult.Success)

        Dim model = New IndexViewModel With {.Email = "new@test.com", .PhoneNumber = "999"}

        ' Act
        Dim result = Await _controller.Index(model)

        ' Assert
        Dim redirect = Assert.IsType(Of RedirectToActionResult)(result)
        Assert.Equal("Index", redirect.ActionName)
    End Function

    <Fact>
    Public Async Function Index_Throws_WhenSetEmailFails() As Task
        Dim user = New ApplicationUser With {.Id = "123", .Email = "old@test.com"}
        _userManager.Setup(Function(u) u.GetUserAsync(It.IsAny(Of ClaimsPrincipal))).ReturnsAsync(user)
        _userManager.Setup(Function(u) u.SetEmailAsync(user, "fail@test.com")).ReturnsAsync(IdentityResult.Failed())

        Dim model = New IndexViewModel With {.Email = "fail@test.com", .PhoneNumber = "123"}

        Await Assert.ThrowsAsync(Of ApplicationException)(Async Function()
                                                              Return Await _controller.Index(model)
                                                          End Function)
    End Function

    <Fact>
    Public Async Function Index_Throws_WhenSetPhoneFails() As Task
        Dim user = New ApplicationUser With {.Id = "123", .PhoneNumber = "123"}
        _userManager.Setup(Function(u) u.GetUserAsync(It.IsAny(Of ClaimsPrincipal))).ReturnsAsync(user)
        _userManager.Setup(Function(u) u.SetPhoneNumberAsync(user, "bad")).ReturnsAsync(IdentityResult.Failed())

        Dim model = New IndexViewModel With {.Email = "test@test.com", .PhoneNumber = "abc"}

        Await Assert.ThrowsAsync(Of NullReferenceException)(Async Function()
                                                                Return Await _controller.Index(model)
                                                            End Function)
    End Function

    <Fact>
    Public Async Function SendVerificationEmail_ReturnsRedirect_WhenValid() As Task
        ' Arrange
        Dim user = New ApplicationUser With {.Id = "1", .Email = "test@example.com"}
        _userManager.Setup(Function(m) m.GetUserAsync(It.IsAny(Of ClaimsPrincipal))).ReturnsAsync(user)
        _userManager.Setup(Function(m) m.GenerateEmailConfirmationTokenAsync(user)).ReturnsAsync("token123")

        Dim urlHelperMock = New Mock(Of IUrlHelper)(MockBehavior.Loose)
        urlHelperMock.Setup(Function(u) u.Action(It.IsAny(Of UrlActionContext))).Returns("http://confirm")
        _controller.Url = urlHelperMock.Object

        Dim model = New IndexViewModel()

        ' Act
        Dim result = Await _controller.SendVerificationEmail(model)

        ' Assert
        Assert.IsType(Of RedirectToActionResult)(result)
    End Function

    <Fact>
    Public Async Function SendVerificationEmail_Throws_WhenUserIsNull() As Task
        _userManager.Setup(Function(m) m.GetUserAsync(It.IsAny(Of ClaimsPrincipal))).ReturnsAsync(CType(Nothing, ApplicationUser))

        Await Assert.ThrowsAsync(Of ApplicationException)(Async Function()
                                                              Return Await _controller.SendVerificationEmail(New IndexViewModel())
                                                          End Function)
    End Function
    <Fact>
    Public Async Function SendVerificationEmail_ReturnsView_WhenModelIsInvalid() As Task
        ' Arrange
        Dim model = New IndexViewModel()

        ' Make ModelState invalid
        _controller.ModelState.AddModelError("Email", "Email is required")

        ' Act
        Dim result = Await _controller.SendVerificationEmail(model)

        ' Assert
        Dim viewResult = Assert.IsType(Of ViewResult)(result)
        Assert.Equal(model, viewResult.Model)
    End Function
    ' creating for change password ( we have to write 3 for get and 4 for posrt methods ) 
    <Fact>
    Public Async Function ChangePassword_Get_Throws_WhenUserIsNull() As Task
        _userManager.Setup(Function(m) m.GetUserAsync(It.IsAny(Of ClaimsPrincipal))).ReturnsAsync(CType(Nothing, ApplicationUser))

        Await Assert.ThrowsAsync(Of ApplicationException)(Async Function()
                                                              Return Await _controller.ChangePassword()
                                                          End Function)
    End Function

    <Fact>
    Public Async Function ChangePassword_Get_Redirects_WhenNoPassword() As Task
        Dim user = New ApplicationUser()
        _userManager.Setup(Function(m) m.GetUserAsync(It.IsAny(Of ClaimsPrincipal))).ReturnsAsync(user)
        _userManager.Setup(Function(m) m.HasPasswordAsync(user)).ReturnsAsync(False)

        Dim result = Await _controller.ChangePassword()

        Dim redirect = Assert.IsType(Of RedirectToActionResult)(result)
        Assert.Equal("SetPassword", redirect.ActionName)
    End Function


    <Fact>
    Public Async Function ChangePassword_Get_ReturnsView_WhenHasPassword() As Task
        Dim user = New ApplicationUser()
        _userManager.Setup(Function(m) m.GetUserAsync(It.IsAny(Of ClaimsPrincipal))).ReturnsAsync(user)
        _userManager.Setup(Function(m) m.HasPasswordAsync(user)).ReturnsAsync(True)

        _controller.StatusMessage = "Password OK"
        Dim result = Await _controller.ChangePassword()

        Dim view = Assert.IsType(Of ViewResult)(result)
        Dim model = Assert.IsType(Of ChangePasswordViewModel)(view.Model)
        Assert.Equal("Password OK", model.StatusMessage)
    End Function

    <Fact>
    Public Async Function ChangePassword_Post_ReturnsView_WhenModelInvalid() As Task
        _controller.ModelState.AddModelError("OldPassword", "Required")

        Dim model = New ChangePasswordViewModel()

        Dim result = Await _controller.ChangePassword(model)

        Dim view = Assert.IsType(Of ViewResult)(result)
        Assert.Equal(model, view.Model)
    End Function

    <Fact>
    Public Async Function ChangePassword_Post_Throws_WhenUserIsNull() As Task
        _userManager.Setup(Function(m) m.GetUserAsync(It.IsAny(Of ClaimsPrincipal))).ReturnsAsync(CType(Nothing, ApplicationUser))

        Dim model = New ChangePasswordViewModel With {
        .OldPassword = "old123", .NewPassword = "new123!"
    }

        Await Assert.ThrowsAsync(Of ApplicationException)(Async Function()
                                                              Return Await _controller.ChangePassword(model)
                                                          End Function)
    End Function

    <Fact>
    Public Async Function ChangePassword_Post_ReturnsView_WhenPasswordChangeFails() As Task
        Dim user = New ApplicationUser()
        _userManager.Setup(Function(m) m.GetUserAsync(It.IsAny(Of ClaimsPrincipal))).ReturnsAsync(user)

        Dim resultErrors = IdentityResult.Failed(New IdentityError With {.Description = "Invalid old password"})
        _userManager.Setup(Function(m) m.ChangePasswordAsync(user, "old123", "new123")).ReturnsAsync(resultErrors)

        Dim model = New ChangePasswordViewModel With {
        .OldPassword = "old123", .NewPassword = "new123"
            }

        Dim result = Await _controller.ChangePassword(model)

        Dim view = Assert.IsType(Of ViewResult)(result)
        Assert.Equal(model, view.Model)
        Assert.False(_controller.ModelState.IsValid)
    End Function

    <Fact>
    Public Async Function ChangePassword_Post_Succeeds_AndRedirects() As Task
        Dim user = New ApplicationUser()
        _userManager.Setup(Function(m) m.GetUserAsync(It.IsAny(Of ClaimsPrincipal))).ReturnsAsync(user)

        _userManager.Setup(Function(m) m.ChangePasswordAsync(user, "old123", "new123")).ReturnsAsync(IdentityResult.Success)
        _signInManager.Setup(Function(s) s.SignInAsync(user, False, Nothing)).Returns(Task.CompletedTask)

        Dim model = New ChangePasswordViewModel With {
        .OldPassword = "old123", .NewPassword = "new123"
    }

        Dim result = Await _controller.ChangePassword(model)

        Dim redirect = Assert.IsType(Of RedirectToActionResult)(result)
        Assert.Equal("ChangePassword", redirect.ActionName)
    End Function

    ' Helpers to mock UserManager & SignInManager
    Private Function MockUserManager() As Mock(Of UserManager(Of ApplicationUser))
        Dim store = New Mock(Of IUserStore(Of ApplicationUser))()
        Return New Mock(Of UserManager(Of ApplicationUser))(store.Object, Nothing, Nothing, Nothing, Nothing, Nothing, Nothing, Nothing, Nothing)
    End Function

    Private Function MockSignInManager(userManager As UserManager(Of ApplicationUser)) As Mock(Of SignInManager(Of ApplicationUser))
        Dim contextAccessor = New Mock(Of IHttpContextAccessor)()
        Dim claimsFactory = New Mock(Of IUserClaimsPrincipalFactory(Of ApplicationUser))()
        Return New Mock(Of SignInManager(Of ApplicationUser))(userManager, contextAccessor.Object, claimsFactory.Object, Nothing, Nothing, Nothing, Nothing)
    End Function

End Class
