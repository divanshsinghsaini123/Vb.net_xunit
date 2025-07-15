using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Identity;
using Microsoft.eShopWeb.Web.Controllers;
using Microsoft.eShopWeb.Web.ViewModels.Account;
using Moq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.Web.Controllers
{
    public class AccountControllerTests
    {
        private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
        private readonly Mock<SignInManager<ApplicationUser>> _mockSignInManager;
        private readonly Mock<IBasketService> _mockBasketService;
        private readonly Mock<IAppLogger<AccountController>> _mockLogger;
        private readonly AccountController _controller;

        public AccountControllerTests()
        {
            // Setup UserManager mock
            var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
            _mockUserManager = new Mock<UserManager<ApplicationUser>>(
                userStoreMock.Object, null, null, null, null, null, null, null, null);

            // Setup SignInManager mock
            var contextAccessorMock = new Mock<IHttpContextAccessor>();
            var userPrincipalFactoryMock = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
            _mockSignInManager = new Mock<SignInManager<ApplicationUser>>(
                _mockUserManager.Object,
                contextAccessorMock.Object,
                userPrincipalFactoryMock.Object,
                null, null, null, null);

            _mockBasketService = new Mock<IBasketService>();
            _mockLogger = new Mock<IAppLogger<AccountController>>();

            _controller = new AccountController(
                _mockUserManager.Object,
                _mockSignInManager.Object,
                _mockBasketService.Object,
                _mockLogger.Object);

            // Setup controller context
            _controller.ControllerContext = new ControllerContext();
            _controller.ControllerContext.HttpContext = new DefaultHttpContext();
            _controller.TempData = new TempDataDictionary(
                new DefaultHttpContext(),
                Mock.Of<ITempDataProvider>());
        }

        #region SIgnIn
        [Fact]
        public async Task SignIn_Get_ShouldSignOutExternalScheme_AndReturnView()
        {
            // Arrange
            var mockAuthService = new Mock<IAuthenticationService>();
            var serviceProviderMock = new Mock<IServiceProvider>();
            serviceProviderMock
                .Setup(x => x.GetService(typeof(IAuthenticationService)))
                .Returns(mockAuthService.Object);
            _controller.ControllerContext.HttpContext.RequestServices = serviceProviderMock.Object;

            // Act
            var result = await _controller.SignIn();

            // Assert
            mockAuthService.Verify(m => m.SignOutAsync(It.IsAny<HttpContext>(),
                "Identity.External", It.IsAny<AuthenticationProperties>()), Times.Once);
            Assert.IsType<ViewResult>(result);
        }

        [Fact]
        public async Task SignIn_Get_WithCheckoutReturnUrl_ShouldSetBasketIndexAsReturnUrl()
        {
            // Arrange
            var mockAuthService = new Mock<IAuthenticationService>();
            var serviceProviderMock = new Mock<IServiceProvider>();
            serviceProviderMock
                .Setup(x => x.GetService(typeof(IAuthenticationService)))
                .Returns(mockAuthService.Object);
            _controller.ControllerContext.HttpContext.RequestServices = serviceProviderMock.Object;

            var returnUrl = "/checkout";

            // Act
            var result = await _controller.SignIn(returnUrl);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal("/Basket/Index", viewResult.ViewData["ReturnUrl"]);
        }

        [Fact]
        public async Task SignIn_Post_WithInvalidModel_ShouldReturnView()
        {
            // Arrange
            var model = new LoginViewModel();
            _controller.ModelState.AddModelError("Error", "Test error");

            // Act
            var result = await _controller.SignIn(model);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(model, viewResult.Model);
        }

        [Fact]
        public async Task SignIn_Post_WithRequiresTwoFactor_ShouldRedirectToLoginWith2fa()
        {
            // Arrange
            var model = new LoginViewModel
            {
                Email = "test@example.com",
                Password = "P@ssw0rd",
                RememberMe = true
            };

            _mockSignInManager
                .Setup(x => x.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, false))
                .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.TwoFactorRequired);

            // Act
            var result = await _controller.SignIn(model);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("LoginWith2fa", redirectResult.ActionName);
            Assert.True((bool)redirectResult.RouteValues["RememberMe"]);
        }

        [Fact]
        public async Task SignIn_Post_WhenSucceeded_WithAnonymousBasket_ShouldTransferBasket()
        {
            // Arrange
            var model = new LoginViewModel
            {
                Email = "test@example.com",
                Password = "P@ssw0rd"
            };
            const string anonymousBasketId = "basket123";

            _mockSignInManager
                .Setup(x => x.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, false))
                .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

            var requestCookiesMock = new Mock<IRequestCookieCollection>();
            requestCookiesMock.Setup(x => x["BASKET_COOKIENAME"]).Returns(anonymousBasketId);
            requestCookiesMock.Setup(x => x["eShopBasket"]).Returns(anonymousBasketId);
            requestCookiesMock.Setup(x => x["Basket"]).Returns(anonymousBasketId);
            requestCookiesMock.Setup(x => x[It.IsAny<string>()]).Returns(anonymousBasketId);

            var responseCookiesMock = new Mock<IResponseCookies>();

            var httpRequest = new Mock<HttpRequest>();
            httpRequest.Setup(r => r.Cookies).Returns(requestCookiesMock.Object);

            var httpResponse = new Mock<HttpResponse>();
            httpResponse.Setup(r => r.Cookies).Returns(responseCookiesMock.Object);

            var httpContext = new Mock<HttpContext>();
            httpContext.Setup(c => c.Request).Returns(httpRequest.Object);
            httpContext.Setup(c => c.Response).Returns(httpResponse.Object);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext.Object
            };
            var urlHelperMock = new Mock<IUrlHelper>();
            urlHelperMock.Setup(x => x.IsLocalUrl(It.IsAny<string>())).Returns(true);
            _controller.Url = urlHelperMock.Object;
            var result = await _controller.SignIn(model, returnUrl: "/");
            _mockBasketService.Verify(x => x.TransferBasketAsync(anonymousBasketId, model.Email), Times.Once);
            responseCookiesMock.Verify(x => x.Delete(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task SignIn_Post_WhenSucceeded_WithLocalReturnUrl_ShouldRedirectToLocal()
        {
            // Arrange
            var model = new LoginViewModel();
            var returnUrl = "/local";

            _mockSignInManager
                .Setup(x => x.PasswordSignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), false))
                .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

            var urlHelperMock = new Mock<IUrlHelper>();
            urlHelperMock.Setup(x => x.IsLocalUrl(returnUrl)).Returns(true);
            _controller.Url = urlHelperMock.Object;

            // Act
            var result = await _controller.SignIn(model, returnUrl);

            // Assert
            var redirectResult = Assert.IsType<RedirectResult>(result);
            Assert.Equal(returnUrl, redirectResult.Url);
        }

        [Fact]
        public async Task SignIn_Post_WhenSucceeded_WithNonLocalReturnUrl_ShouldRedirectToIndex()
        {
            // Arrange
            var model = new LoginViewModel();
            var returnUrl = "http://external.com";

            _mockSignInManager
                .Setup(x => x.PasswordSignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), false))
                .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

            var urlHelperMock = new Mock<IUrlHelper>();
            urlHelperMock.Setup(x => x.IsLocalUrl(returnUrl)).Returns(false);
            _controller.Url = urlHelperMock.Object;

            // Act
            var result = await _controller.SignIn(model, returnUrl);

            // Assert
            var redirectResult = Assert.IsType<RedirectToPageResult>(result);
            Assert.Equal("/Index", redirectResult.PageName);
        }

        [Fact]
        public async Task SignIn_Post_WhenFailed_ShouldAddModelError()
        {
            // Arrange
            var model = new LoginViewModel();

            _mockSignInManager
                .Setup(x => x.PasswordSignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), false))
                .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Failed);

            // Act
            var result = await _controller.SignIn(model);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(model, viewResult.Model);
            Assert.True(_controller.ModelState.ContainsKey(string.Empty));
            Assert.Equal("Invalid login attempt.", _controller.ModelState[string.Empty].Errors[0].ErrorMessage);
        }

        [Fact]
        public async Task LoginWith2fa_Get_UserNull_ShouldThrowApplicationException()
        {
            // Arrange
            _mockSignInManager
                .Setup(x => x.GetTwoFactorAuthenticationUserAsync())
                .ReturnsAsync((ApplicationUser)null);

            // Act & Assert
            await Assert.ThrowsAsync<ApplicationException>(() => _controller.LoginWith2fa(false));
        }

        [Fact]
        public async Task LoginWith2fa_Get_ShouldReturnView()
        {
            // Arrange
            _mockSignInManager
                .Setup(x => x.GetTwoFactorAuthenticationUserAsync())
                .ReturnsAsync(new ApplicationUser());

            // Act
            var result = await _controller.LoginWith2fa(true);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<LoginWith2faViewModel>(viewResult.Model);
            Assert.True(model.RememberMe);
        }

        [Fact]
        public async Task LoginWith2fa_Post_WithInvalidModel_ShouldReturnView()
        {
            // Arrange
            var model = new LoginWith2faViewModel();
            _controller.ModelState.AddModelError("Error", "Test error");

            // Act
            var result = await _controller.LoginWith2fa(model, false);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(model, viewResult.Model);
        }

        [Fact]
        public async Task LoginWith2fa_Post_UserNull_ShouldThrowApplicationException()
        {
            // Arrange
            var model = new LoginWith2faViewModel { TwoFactorCode = "123456" };

            _mockSignInManager
                .Setup(x => x.GetTwoFactorAuthenticationUserAsync())
                .ReturnsAsync(new ApplicationUser());


            var userPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
            _controller.ControllerContext.HttpContext = new DefaultHttpContext
            {
                User = userPrincipal
            };
            await Assert.ThrowsAsync<NullReferenceException>(() => _controller.LoginWith2fa(model, false));
        }

        [Fact]
        public async Task LoginWith2fa_Post_WhenSucceeded_ShouldRedirectToLocal()
        {
            // Arrange
            var model = new LoginWith2faViewModel { TwoFactorCode = "123456" };
            var returnUrl = "/local";
            var user = new ApplicationUser { Id = "user1" };

            _mockSignInManager
                .Setup(x => x.GetTwoFactorAuthenticationUserAsync())
                .ReturnsAsync(user);

            var userPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "user1")
            }));
            _controller.ControllerContext.HttpContext.User = userPrincipal;

            _mockUserManager
                .Setup(x => x.GetUserId(It.IsAny<ClaimsPrincipal>()))
                .Returns("user1");

            _mockSignInManager
                .Setup(x => x.TwoFactorAuthenticatorSignInAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

            var urlHelperMock = new Mock<IUrlHelper>();
            urlHelperMock.Setup(x => x.IsLocalUrl(returnUrl)).Returns(true);
            _controller.Url = urlHelperMock.Object;

            // Act
            var result = await _controller.LoginWith2fa(model, false, returnUrl);

            // Assert
            var redirectResult = Assert.IsType<RedirectResult>(result);
            Assert.Equal(returnUrl, redirectResult.Url);
            _mockLogger.Verify(
                x => x.LogInformation(It.Is<string>(s => s.Contains("logged in with 2fa")), user.Id),
                Times.Once);
        }

        [Fact]
        public async Task LoginWith2fa_Post_WhenLockedOut_ShouldRedirectToLockout()
        {
            // Arrange
            var model = new LoginWith2faViewModel { TwoFactorCode = "123456" };
            var user = new ApplicationUser { Id = "user1" };

            _mockSignInManager
                .Setup(x => x.GetTwoFactorAuthenticationUserAsync())
                .ReturnsAsync(user);

            var userPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "user1")
            }));
            _controller.ControllerContext.HttpContext.User = userPrincipal;

            _mockUserManager
                .Setup(x => x.GetUserId(It.IsAny<ClaimsPrincipal>()))
                .Returns("user1");

            _mockSignInManager
                .Setup(x => x.TwoFactorAuthenticatorSignInAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.LockedOut);

            // Act
            var result = await _controller.LoginWith2fa(model, false);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Lockout", redirectResult.ActionName);
            _mockLogger.Verify(
                x => x.LogWarning(It.Is<string>(s => s.Contains("account locked out")), user.Id),
                Times.Once);
        }

        [Fact]
        public async Task LoginWith2fa_Post_WhenFailed_ShouldAddModelError()
        {
            // Arrange
            var model = new LoginWith2faViewModel { TwoFactorCode = "123456" };
            var user = new ApplicationUser { Id = "user1" };

            _mockSignInManager
                .Setup(x => x.GetTwoFactorAuthenticationUserAsync())
                .ReturnsAsync(user);

            var userPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "user1")
            }));
            _controller.ControllerContext.HttpContext.User = userPrincipal;

            _mockUserManager
                .Setup(x => x.GetUserId(It.IsAny<ClaimsPrincipal>()))
                .Returns("user1");

            _mockSignInManager
                .Setup(x => x.TwoFactorAuthenticatorSignInAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Failed);

            // Act
            var result = await _controller.LoginWith2fa(model, false);

            // Assert
            Assert.IsType<ViewResult>(result);
            Assert.True(_controller.ModelState.ContainsKey(string.Empty));
            Assert.Equal("Invalid authenticator code.", _controller.ModelState[string.Empty].Errors[0].ErrorMessage);
            _mockLogger.Verify(
                x => x.LogWarning(It.Is<string>(s => s.Contains("Invalid authenticator code")), user.Id),
                Times.Once);
        }

        [Fact]
        public void Lockout_ShouldReturnView()
        {
            // Act
            var result = _controller.Lockout();

            // Assert
            Assert.IsType<ViewResult>(result);
        }
        #endregion

        #region SIGNOUT
        [Fact]
        public async Task SignOut_ShouldSignOutAndRedirect()
        {
            // Act
            var result = await _controller.SignOut();

            // Assert
            _mockSignInManager.Verify(x => x.SignOutAsync(), Times.Once);
            var redirectResult = Assert.IsType<RedirectToPageResult>(result);
            Assert.Equal("/Index", redirectResult.PageName);
        }
        #endregion

        #region Register
        [Fact]
        public void Register_Get_ShouldReturnView()
        {
            // Act
            var result = _controller.Register();

            // Assert
            Assert.IsType<ViewResult>(result);
        }

        [Fact]
        public async Task Register_Post_WithInvalidModel_ShouldReturnView()
        {
            // Arrange
            var model = new RegisterViewModel();
            _controller.ModelState.AddModelError("Error", "Test error");

            // Act
            var result = await _controller.Register(model);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(model, viewResult.Model);
        }

        [Fact]
        public async Task Register_Post_WhenSucceeded_ShouldSignInAndRedirect()
        {
            // Arrange
            var model = new RegisterViewModel
            {
                Email = "test@example.com",
                Password = "P@ssw0rd"
            };
            var returnUrl = "/local";

            _mockUserManager
                .Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), model.Password))
                .ReturnsAsync(IdentityResult.Success);

            var urlHelperMock = new Mock<IUrlHelper>();
            urlHelperMock.Setup(x => x.IsLocalUrl(returnUrl)).Returns(true);
            _controller.Url = urlHelperMock.Object;

            // Act
            var result = await _controller.Register(model, returnUrl);

            // Assert
            _mockUserManager.Verify(
                x => x.CreateAsync(
                    It.Is<ApplicationUser>(u => u.Email == model.Email && u.UserName == model.Email),
                    model.Password),
                Times.Once);

            _mockSignInManager.Verify(
                x => x.SignInAsync(
                    It.Is<ApplicationUser>(u => u.Email == model.Email),
                    false,
                    null),
                Times.Once);

            var redirectResult = Assert.IsType<RedirectResult>(result);
            Assert.Equal(returnUrl, redirectResult.Url);
        }

        [Fact]
        public async Task Register_Post_WhenFailed_ShouldAddErrors()
        {
            // Arrange
            var model = new RegisterViewModel
            {
                Email = "test@example.com",
                Password = "P@ssw0rd"
            };

            var identityErrors = new List<IdentityError>
            {
                new IdentityError { Description = "Error 1" },
                new IdentityError { Description = "Error 2" }
            };

            _mockUserManager
                .Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), model.Password))
                .ReturnsAsync(IdentityResult.Failed(identityErrors.ToArray()));

            // Act
            var result = await _controller.Register(model);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(model, viewResult.Model);
            Assert.Equal(2, _controller.ModelState.ErrorCount);
            Assert.Equal("Error 1", _controller.ModelState[""].Errors[0].ErrorMessage);
            Assert.Equal("Error 2", _controller.ModelState[""].Errors[1].ErrorMessage);
        }

        #endregion

        #region CONFIRM_EMAIL

        [Fact]
        public async Task ConfirmEmail_WithNullParameters_ShouldRedirectToIndex()
        {
            // Act
            var result = await _controller.ConfirmEmail(null, null);

            // Assert
            var redirectResult = Assert.IsType<RedirectToPageResult>(result);
            Assert.Equal("/Index", redirectResult.PageName);
        }

        [Fact]
        public async Task ConfirmEmail_UserNotFound_ShouldThrowApplicationException()
        {
            // Arrange
            _mockUserManager
                .Setup(x => x.FindByIdAsync("userId"))
                .ReturnsAsync((ApplicationUser)null);

            // Act & Assert
            await Assert.ThrowsAsync<ApplicationException>(() => _controller.ConfirmEmail("userId", "code"));
        }

        [Fact]
        public async Task ConfirmEmail_WhenSucceeded_ShouldReturnConfirmEmailView()
        {
            // Arrange
            var user = new ApplicationUser();

            _mockUserManager
                .Setup(x => x.FindByIdAsync("userId"))
                .ReturnsAsync(user);

            _mockUserManager
                .Setup(x => x.ConfirmEmailAsync(user, "code"))
                .ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _controller.ConfirmEmail("userId", "code");

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal("ConfirmEmail", viewResult.ViewName);
        }

        [Fact]
        public async Task ConfirmEmail_WhenFailed_ShouldReturnErrorView()
        {
            // Arrange
            var user = new ApplicationUser();

            _mockUserManager
                .Setup(x => x.FindByIdAsync("userId"))
                .ReturnsAsync(user);

            _mockUserManager
                .Setup(x => x.ConfirmEmailAsync(user, "code"))
                .ReturnsAsync(IdentityResult.Failed());

            // Act
            var result = await _controller.ConfirmEmail("userId", "code");

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal("Error", viewResult.ViewName);
        }

        #endregion

        #region RESET_PASSWORD

        [Fact]
        public void ResetPassword_WithNullCode_ShouldThrowApplicationException()
        {
            // Act & Assert
            Assert.Throws<ApplicationException>(() => _controller.ResetPassword(null));
        }

        [Fact]
        public void ResetPassword_WithValidCode_ShouldReturnView()
        {
            // Arrange
            var code = "resetCode";

            // Act
            var result = _controller.ResetPassword(code);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<ResetPasswordViewModel>(viewResult.Model);
            Assert.Equal(code, model.Code);
        }
        #endregion
    }
}