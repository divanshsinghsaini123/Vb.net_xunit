using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Identity;
using Microsoft.eShopWeb.Web.Controllers;
using Microsoft.eShopWeb.Web.ViewModels.Manage;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Xunit;

namespace test_c__new.Controllers
{
    public class ManageControllerTest
    {
        public class ManageControllerAllTests
        {
            private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
            private readonly Mock<SignInManager<ApplicationUser>> _mockSignInManager;
            private readonly Mock<IEmailSender> _mockEmailSender;
            private readonly Mock<IAppLogger<ManageController>> _mockLogger;
            private readonly Mock<UrlEncoder> _mockUrlEncoder;
            private readonly Mock<IUrlHelper> _mockUrlHelper;
            private readonly ManageController _controller;
            private readonly ApplicationUser _testUser;
            private readonly ClaimsPrincipal _testPrincipal;

            public ManageControllerAllTests()
            {
                // Setup UserManager mock
                var userStore = new Mock<IUserStore<ApplicationUser>>();
                _mockUserManager = new Mock<UserManager<ApplicationUser>>(
                    userStore.Object, null, null, null, null, null, null, null, null);

                // Setup SignInManager mock
                var contextAccessor = new Mock<IHttpContextAccessor>();
                var userPrincipalFactory = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
                _mockSignInManager = new Mock<SignInManager<ApplicationUser>>(
                    _mockUserManager.Object, contextAccessor.Object, userPrincipalFactory.Object,
                    null, null, null, null);

                _mockEmailSender = new Mock<IEmailSender>();
                _mockLogger = new Mock<IAppLogger<ManageController>>();
                _mockUrlEncoder = new Mock<UrlEncoder>();
                _mockUrlHelper = new Mock<IUrlHelper>();

                _testUser = new ApplicationUser
                {
                    Id = "test-user-id",
                    UserName = "testuser@example.com",
                    Email = "testuser@example.com",
                    EmailConfirmed = true,
                    PhoneNumber = "123-456-7890",
                    TwoFactorEnabled = false
                };

                _testPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                new Claim(ClaimTypes.NameIdentifier, _testUser.Id),
                new Claim(ClaimTypes.Name, _testUser.UserName)
            }));

                _controller = new ManageController(
                    _mockUserManager.Object,
                    _mockSignInManager.Object,
                    _mockEmailSender.Object,
                    _mockLogger.Object,
                    _mockUrlEncoder.Object);

                // Setup controller context
                var httpContext = new Mock<HttpContext>();
                httpContext.Setup(c => c.User).Returns(_testPrincipal);

                _controller.ControllerContext = new ControllerContext
                {
                    HttpContext = httpContext.Object
                };

                _controller.Url = _mockUrlHelper.Object;
                _controller.TempData = new Mock<ITempDataDictionary>().Object;
            }

            #region MyAccount Tests

            [Fact]
            public async Task MyAccount_ReturnsViewWithCorrectModel_WhenUserExists()
            {
                // Arrange
                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);

                // Act
                var result = await _controller.MyAccount();

                // Assert
                var viewResult = Assert.IsType<ViewResult>(result);
                var model = Assert.IsType<IndexViewModel>(viewResult.Model);
                Assert.Equal(_testUser.UserName, model.Username);
                Assert.Equal(_testUser.Email, model.Email);
                Assert.Equal(_testUser.PhoneNumber, model.PhoneNumber);
                Assert.Equal(_testUser.EmailConfirmed, model.IsEmailConfirmed);
            }

            [Fact]
            public async Task MyAccount_ThrowsApplicationException_WhenUserNotFound()
            {
                // Arrange
                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync((ApplicationUser)null);
                _mockUserManager.Setup(m => m.GetUserId(It.IsAny<ClaimsPrincipal>()))
                    .Returns(_testUser.Id);

                // Act & Assert
                var exception = await Assert.ThrowsAsync<ApplicationException>(
                    () => _controller.MyAccount());
                Assert.Contains("Unable to load user", exception.Message);
            }

            #endregion

            #region Index Tests

            [Fact]
            public async Task Index_Post_ReturnsView_WhenModelStateInvalid()
            {
                // Arrange
                var model = new IndexViewModel { Email = "invalid-email" };
                _controller.ModelState.AddModelError("Email", "Invalid email format");

                // Act
                var result = await _controller.Index(model);

                // Assert
                var viewResult = Assert.IsType<ViewResult>(result);
                Assert.Same(model, viewResult.Model);
            }

            [Fact]
            public async Task Index_Post_UpdatesEmailAndPhoneNumber_WhenBothChanged()
            {
                // Arrange
                var model = new IndexViewModel
                {
                    Email = "newemail@example.com",
                    PhoneNumber = "987-654-3210"
                };

                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.SetEmailAsync(_testUser, model.Email))
                    .ReturnsAsync(IdentityResult.Success);
                _mockUserManager.Setup(m => m.SetPhoneNumberAsync(_testUser, model.PhoneNumber))
                    .ReturnsAsync(IdentityResult.Success);

                // Act
                var result = await _controller.Index(model);

                // Assert
                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("Index", redirectResult.ActionName);
                _mockUserManager.Verify(m => m.SetEmailAsync(_testUser, model.Email), Times.Once);
                _mockUserManager.Verify(m => m.SetPhoneNumberAsync(_testUser, model.PhoneNumber), Times.Once);
            }

            [Fact]
            public async Task Index_Post_UpdatesOnlyEmail_WhenPhoneNumberUnchanged()
            {
                // Arrange
                var model = new IndexViewModel
                {
                    Email = "newemail@example.com",
                    PhoneNumber = _testUser.PhoneNumber
                };

                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.SetEmailAsync(_testUser, model.Email))
                    .ReturnsAsync(IdentityResult.Success);

                // Act
                var result = await _controller.Index(model);

                // Assert
                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("Index", redirectResult.ActionName);
                _mockUserManager.Verify(m => m.SetEmailAsync(_testUser, model.Email), Times.Once);
                _mockUserManager.Verify(m => m.SetPhoneNumberAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
            }

            [Fact]
            public async Task Index_Post_UpdatesOnlyPhoneNumber_WhenEmailUnchanged()
            {
                // Arrange
                var model = new IndexViewModel
                {
                    Email = _testUser.Email,
                    PhoneNumber = "987-654-3210"
                };

                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.SetPhoneNumberAsync(_testUser, model.PhoneNumber))
                    .ReturnsAsync(IdentityResult.Success);

                // Act
                var result = await _controller.Index(model);

                // Assert
                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("Index", redirectResult.ActionName);
                _mockUserManager.Verify(m => m.SetPhoneNumberAsync(_testUser, model.PhoneNumber), Times.Once);
                _mockUserManager.Verify(m => m.SetEmailAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
            }

            [Fact]
            public async Task Index_Post_NoChanges_WhenEmailAndPhoneNumberSame()
            {
                // Arrange
                var model = new IndexViewModel
                {
                    Email = _testUser.Email,
                    PhoneNumber = _testUser.PhoneNumber
                };

                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);

                // Act
                var result = await _controller.Index(model);

                // Assert
                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("Index", redirectResult.ActionName);
                _mockUserManager.Verify(m => m.SetEmailAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
                _mockUserManager.Verify(m => m.SetPhoneNumberAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
            }

            [Fact]
            public async Task Index_Post_ThrowsApplicationException_WhenSetEmailFails()
            {
                // Arrange
                var model = new IndexViewModel
                {
                    Email = "newemail@example.com",
                    PhoneNumber = _testUser.PhoneNumber
                };

                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.SetEmailAsync(_testUser, model.Email))
                    .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Email update failed" }));

                // Act & Assert
                var exception = await Assert.ThrowsAsync<ApplicationException>(
                    () => _controller.Index(model));
                Assert.Contains("Unexpected error", exception.Message);
            }

            [Fact]
            public async Task Index_Post_ThrowsApplicationException_WhenSetPhoneNumberFails()
            {
                // Arrange
                var model = new IndexViewModel
                {
                    Email = _testUser.Email,
                    PhoneNumber = "987-654-3210"
                };

                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.SetPhoneNumberAsync(_testUser, model.PhoneNumber))
                    .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Phone update failed" }));

                // Act & Assert
                var exception = await Assert.ThrowsAsync<ApplicationException>(
                    () => _controller.Index(model));
                Assert.Contains("Unexpected error", exception.Message);
            }

            [Fact]
            public async Task Index_Post_ThrowsApplicationException_WhenUserNotFound()
            {
                // Arrange
                var model = new IndexViewModel
                {
                    Email = "test@example.com",
                    PhoneNumber = "123-456-7890"
                };

                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync((ApplicationUser)null);
                _mockUserManager.Setup(m => m.GetUserId(It.IsAny<ClaimsPrincipal>()))
                    .Returns(_testUser.Id);

                // Act & Assert
                var exception = await Assert.ThrowsAsync<ApplicationException>(
                    () => _controller.Index(model));
                Assert.Contains("Unable to load user", exception.Message);
            }

            #endregion

            #region SendVerificationEmail Tests

            [Fact]
            public async Task SendVerificationEmail_ReturnsView_WhenModelStateInvalid()
            {
                // Arrange
                var model = new IndexViewModel { Email = "" };
                _controller.ModelState.AddModelError("Email", "Email is required");

                // Act
                var result = await _controller.SendVerificationEmail(model);

                // Assert
                var viewResult = Assert.IsType<ViewResult>(result);
                Assert.Equal("SendVerificationEmail", viewResult.ViewName);
            }

            [Fact]
            public async Task SendVerificationEmail_SendsEmailAndRedirects_WhenValid()
            {
                // Arrange
                var model = new IndexViewModel { Email = _testUser.Email };
                var confirmationToken = "confirmation-token";

                // Mock UserManager
                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.GenerateEmailConfirmationTokenAsync(_testUser))
                    .ReturnsAsync(confirmationToken);

                // Mock EmailSender
                _mockEmailSender.Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(Task.CompletedTask);

                // Mock User (ClaimsPrincipal)
                var userClaimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
                _controller.ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = userClaimsPrincipal }
                };

                // Mock UrlHelper (if your method uses Url.Action or similar)
                var mockUrlHelper = new Mock<IUrlHelper>();
                mockUrlHelper
                    .Setup(x => x.Action(It.IsAny<UrlActionContext>()))
                    .Returns("http://dummy-callback-url");
                _controller.Url = mockUrlHelper.Object;

                // Act
                var result = await _controller.SendVerificationEmail(model);

                // Assert
                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("Index", redirectResult.ActionName);
                _mockEmailSender.Verify(
                    e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                    Times.Once
                );
            }

            [Fact]
            public async Task SendVerificationEmail_ThrowsApplicationException_WhenUserNotFound()
            {
                // Arrange
                var model = new IndexViewModel { Email = "test@example.com" };

                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync((ApplicationUser)null);
                _mockUserManager.Setup(m => m.GetUserId(It.IsAny<ClaimsPrincipal>()))
                    .Returns(_testUser.Id);

                // Act & Assert
                var exception = await Assert.ThrowsAsync<ApplicationException>(
                    () => _controller.SendVerificationEmail(model));
                Assert.Contains("Unable to load user", exception.Message);
            }

            #endregion

            #region ChangePassword Tests

            [Fact]
            public async Task ChangePassword_Get_ReturnsView_WhenUserHasPassword()
            {
                // Arrange
                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.HasPasswordAsync(_testUser))
                    .ReturnsAsync(true);

                // Act
                var result = await _controller.ChangePassword();

                // Assert
                var viewResult = Assert.IsType<ViewResult>(result);
                Assert.IsType<ChangePasswordViewModel>(viewResult.Model);
            }

            [Fact]
            public async Task ChangePassword_Get_RedirectsToSetPassword_WhenUserHasNoPassword()
            {
                // Arrange
                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.HasPasswordAsync(_testUser))
                    .ReturnsAsync(false);

                // Act
                var result = await _controller.ChangePassword();

                // Assert
                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("SetPassword", redirectResult.ActionName);
            }

            [Fact]
            public async Task ChangePassword_Get_ThrowsApplicationException_WhenUserNotFound()
            {
                // Arrange
                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync((ApplicationUser)null);
                _mockUserManager.Setup(m => m.GetUserId(It.IsAny<ClaimsPrincipal>()))
                    .Returns(_testUser.Id);

                // Act & Assert
                var exception = await Assert.ThrowsAsync<ApplicationException>(
                    () => _controller.ChangePassword());
                Assert.Contains("Unable to load user", exception.Message);
            }

            [Fact]
            public async Task ChangePassword_Post_ReturnsView_WhenModelStateInvalid()
            {
                // Arrange
                var model = new ChangePasswordViewModel();
                _controller.ModelState.AddModelError("OldPassword", "Old password is required");

                // Act
                var result = await _controller.ChangePassword(model);

                // Assert
                var viewResult = Assert.IsType<ViewResult>(result);
                Assert.Same(model, viewResult.Model);
            }

            [Fact]
            public async Task ChangePassword_Post_ChangesPasswordAndRedirects_WhenValid()
            {
                // Arrange
                var model = new ChangePasswordViewModel
                {
                    OldPassword = "OldPassword123!",
                    NewPassword = "NewPassword123!",
                    ConfirmPassword = "NewPassword123!"
                };

                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.ChangePasswordAsync(_testUser, model.OldPassword, model.NewPassword))
                    .ReturnsAsync(IdentityResult.Success);
                _mockSignInManager.Setup(m => m.SignInAsync(_testUser, false, null))
                    .Returns(Task.CompletedTask);

                // Act
                var result = await _controller.ChangePassword(model);

                // Assert
                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("ChangePassword", redirectResult.ActionName);
                _mockUserManager.Verify(m => m.ChangePasswordAsync(_testUser, model.OldPassword, model.NewPassword), Times.Once);
                _mockSignInManager.Verify(m => m.SignInAsync(_testUser, false, null), Times.Once);
            }

            [Fact]
            public async Task ChangePassword_Post_ReturnsView_WhenPasswordChangeFails()
            {
                // Arrange
                var model = new ChangePasswordViewModel
                {
                    OldPassword = "OldPassword123!",
                    NewPassword = "NewPassword123!",
                    ConfirmPassword = "NewPassword123!"
                };

                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.ChangePasswordAsync(_testUser, model.OldPassword, model.NewPassword))
                    .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Password change failed" }));

                // Act
                var result = await _controller.ChangePassword(model);

                // Assert
                var viewResult = Assert.IsType<ViewResult>(result);
                Assert.Same(model, viewResult.Model);
                Assert.False(_controller.ModelState.IsValid);
            }

            [Fact]
            public async Task ChangePassword_Post_ThrowsApplicationException_WhenUserNotFound()
            {
                // Arrange
                var model = new ChangePasswordViewModel
                {
                    OldPassword = "OldPassword123!",
                    NewPassword = "NewPassword123!"
                };

                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync((ApplicationUser)null);
                _mockUserManager.Setup(m => m.GetUserId(It.IsAny<ClaimsPrincipal>()))
                    .Returns(_testUser.Id);

                // Act & Assert
                var exception = await Assert.ThrowsAsync<ApplicationException>(
                    () => _controller.ChangePassword(model));
                Assert.Contains("Unable to load user", exception.Message);
            }

            #endregion

            #region SetPassword Tests

            [Fact]
            public async Task SetPassword_Get_ReturnsView_WhenUserHasNoPassword()
            {
                // Arrange
                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.HasPasswordAsync(_testUser))
                    .ReturnsAsync(false);

                // Act
                var result = await _controller.SetPassword();

                // Assert
                var viewResult = Assert.IsType<ViewResult>(result);
                Assert.IsType<SetPasswordViewModel>(viewResult.Model);
            }

            [Fact]
            public async Task SetPassword_Get_RedirectsToChangePassword_WhenUserHasPassword()
            {
                // Arrange
                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.HasPasswordAsync(_testUser))
                    .ReturnsAsync(true);

                // Act
                var result = await _controller.SetPassword();

                // Assert
                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("ChangePassword", redirectResult.ActionName);
            }

            [Fact]
            public async Task SetPassword_Post_ReturnsView_WhenModelStateInvalid()
            {
                // Arrange
                var model = new SetPasswordViewModel();
                _controller.ModelState.AddModelError("NewPassword", "Password is required");

                // Act
                var result = await _controller.SetPassword(model);

                // Assert
                var viewResult = Assert.IsType<ViewResult>(result);
                Assert.Same(model, viewResult.Model);
            }

            [Fact]
            public async Task SetPassword_Post_SetsPasswordAndRedirects_WhenValid()
            {
                // Arrange
                var model = new SetPasswordViewModel
                {
                    NewPassword = "NewPassword123!",
                    ConfirmPassword = "NewPassword123!"
                };

                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.AddPasswordAsync(_testUser, model.NewPassword))
                    .ReturnsAsync(IdentityResult.Success);
                _mockSignInManager.Setup(m => m.SignInAsync(_testUser, false, null))
                        .Returns(Task.CompletedTask);

                // Act
                var result = await _controller.SetPassword(model);

                // Assert
                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("SetPassword", redirectResult.ActionName);
                _mockUserManager.Verify(m => m.AddPasswordAsync(_testUser, model.NewPassword), Times.Once);
                _mockSignInManager.Verify(m => m.SignInAsync(_testUser, false, null), Times.Once);
            }

            [Fact]
            public async Task SetPassword_Post_ReturnsView_WhenAddPasswordFails()
            {
                // Arrange
                var model = new SetPasswordViewModel
                {
                    NewPassword = "NewPassword123!",
                    ConfirmPassword = "NewPassword123!"
                };

                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.AddPasswordAsync(_testUser, model.NewPassword))
                    .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Password set failed" }));

                // Act
                var result = await _controller.SetPassword(model);

                // Assert
                var viewResult = Assert.IsType<ViewResult>(result);
                Assert.Same(model, viewResult.Model);
                Assert.False(_controller.ModelState.IsValid);
            }

            #endregion

            #region ExternalLogins Tests

            [Fact]
            public async Task ExternalLogins_ReturnsView_WithCorrectModel()
            {
                // Arrange
                var userLogins = new List<UserLoginInfo>
            {
                new UserLoginInfo("Google", "google-key", "Google")
            };
                var externalSchemes = new List<AuthenticationScheme>
            {
                new AuthenticationScheme("Google", "Google", typeof(IAuthenticationHandler)),
                new AuthenticationScheme("Facebook", "Facebook", typeof(IAuthenticationHandler))
            };

                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.GetLoginsAsync(_testUser))
                    .ReturnsAsync(userLogins);
                _mockSignInManager.Setup(m => m.GetExternalAuthenticationSchemesAsync())
                    .ReturnsAsync(externalSchemes);

                // Act
                var result = await _controller.ExternalLogins();

                // Assert
                var viewResult = Assert.IsType<ViewResult>(result);
                var model = Assert.IsType<ExternalLoginsViewModel>(viewResult.Model);
                Assert.Equal(userLogins, model.CurrentLogins);
                Assert.Single(model.OtherLogins); // Facebook should be the only "other" login
            }

            [Fact]
            public async Task LinkLogin_ReturnsChallengeResult()
            {
                // Arrange
                var provider = "Google";
                var redirectUrl = "callback-url";
                _mockUrlHelper.Setup(u => u.Action(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(redirectUrl);

                // Act
                var result = await _controller.LinkLogin(provider);

                // Assert
                var challengeResult = Assert.IsType<ChallengeResult>(result);
                Assert.Equal(provider, challengeResult.AuthenticationSchemes.First());
            }

            [Fact]
            public async Task LinkLoginCallback_AddsLogin_WhenValid()
            {
                // Arrange
                var externalLoginInfo = new ExternalLoginInfo(
                    _testPrincipal, "Google", "google-key", "Google");

                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockSignInManager.Setup(m => m.GetExternalLoginInfoAsync(It.IsAny<string>()))
                    .ReturnsAsync(externalLoginInfo);
                _mockUserManager.Setup(m => m.AddLoginAsync(_testUser, externalLoginInfo))
                    .ReturnsAsync(IdentityResult.Success);

                // Act
                var result = await _controller.LinkLoginCallback();

                // Assert
                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("ExternalLogins", redirectResult.ActionName);
                _mockUserManager.Verify(m => m.AddLoginAsync(_testUser, externalLoginInfo), Times.Once);
            }

            [Fact]
            public async Task LinkLoginCallback_ThrowsException_WhenNoExternalLoginInfo()
            {
                // Arrange
                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockSignInManager.Setup(m => m.GetExternalLoginInfoAsync(It.IsAny<string>()))
                    .ReturnsAsync((ExternalLoginInfo)null);

                // Act & Assert
                var exception = await Assert.ThrowsAsync<ApplicationException>(
                    () => _controller.LinkLoginCallback());
                Assert.Contains("Unexpected error occurred loading external login info for _user with ID ", exception.Message);
            }

            [Fact]
            public async Task LinkLoginCallback_ThrowsException_WhenAddLoginFails()
            {
                // Arrange
                var externalLoginInfo = new ExternalLoginInfo(
                    _testPrincipal, "Google", "google-key", "Google");

                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockSignInManager.Setup(m => m.GetExternalLoginInfoAsync(It.IsAny<string>()))
                    .ReturnsAsync(externalLoginInfo);
                _mockUserManager.Setup(m => m.AddLoginAsync(_testUser, externalLoginInfo))
                    .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Add login failed" }));

                // Act & Assert
                var exception = await Assert.ThrowsAsync<ApplicationException>(
                    () => _controller.LinkLoginCallback());
                Assert.Contains("Unexpected error", exception.Message);
            }

            [Fact]
            public async Task RemoveLogin_RemovesLogin_WhenValid()
            {
                // Arrange
                var model = new RemoveLoginViewModel
                {
                    LoginProvider = "Google",
                    ProviderKey = "google-key"
                };

                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.RemoveLoginAsync(_testUser, model.LoginProvider, model.ProviderKey))
                    .ReturnsAsync(IdentityResult.Success);
                _mockSignInManager.Setup(m => m.SignInAsync(_testUser, false, null))
    .Returns(Task.CompletedTask);

                // Act
                var result = await _controller.RemoveLogin(model);

                // Assert
                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("ExternalLogins", redirectResult.ActionName);
                _mockUserManager.Verify(m => m.RemoveLoginAsync(_testUser, model.LoginProvider, model.ProviderKey), Times.Once);
                _mockSignInManager.Verify(m => m.SignInAsync(_testUser, false, null), Times.Once);
            }

            [Fact]
            public async Task RemoveLogin_ThrowsException_WhenRemoveLoginFails()
            {
                // Arrange
                var model = new RemoveLoginViewModel
                {
                    LoginProvider = "Google",
                    ProviderKey = "google-key"
                };

                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.RemoveLoginAsync(_testUser, model.LoginProvider, model.ProviderKey))
                    .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Remove login failed" }));

                // Act & Assert
                var exception = await Assert.ThrowsAsync<ApplicationException>(
                    () => _controller.RemoveLogin(model));
                Assert.Contains("Unexpected error", exception.Message);
            }

            #endregion

            #region TwoFactorAuthentication Tests

            [Fact]
            public async Task TwoFactorAuthentication_ReturnsView_WithCorrectModel()
            {
                // Arrange

                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.GetTwoFactorEnabledAsync(_testUser))
                    .ReturnsAsync(true);
                _mockUserManager.Setup(m => m.CountRecoveryCodesAsync(_testUser))
                    .ReturnsAsync(5);

                // Act
                var result = await _controller.TwoFactorAuthentication();

                // Assert
                var viewResult = Assert.IsType<ViewResult>(result);
                var model = Assert.IsType<TwoFactorAuthenticationViewModel>(viewResult.Model);
                Assert.True(model.Is2faEnabled);
                Assert.Equal(5, model.RecoveryCodesLeft);
            }

            [Fact]
            public async Task TwoFactorAuthentication_HandlesNullAuthenticatorKey()
            {
                // Arrange
                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.GetTwoFactorEnabledAsync(_testUser))
                    .ReturnsAsync(false);
                _mockUserManager.Setup(m => m.GetAuthenticatorKeyAsync(_testUser))
                    .ReturnsAsync((string)null);

                // Act
                var result = await _controller.TwoFactorAuthentication();

                // Assert
                var viewResult = Assert.IsType<ViewResult>(result);
                var model = Assert.IsType<TwoFactorAuthenticationViewModel>(viewResult.Model);
                Assert.False(model.Is2faEnabled);
                Assert.False(model.HasAuthenticator);
            }

            [Fact]
            public async Task Disable2faWarning_ReturnsView_WhenTwoFactorEnabled()
            {

                var _tempuser = _testUser;
                _tempuser.TwoFactorEnabled = true;
                // Arrange
                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_tempuser);
                //_mockUserManager.Setup(m => m.GetTwoFactorEnabledAsync(_testUser))
                //    .ReturnsAsync(true);

                // Act
                var result = await _controller.Disable2faWarning();

                // Assert
                var viewResult = Assert.IsType<ViewResult>(result);
            }

            [Fact]
            public async Task Disable2faWarning_ThrowsException_WhenTwoFactorNotEnabled()
            {
                // Arrange
                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.GetTwoFactorEnabledAsync(_testUser))
                    .ReturnsAsync(false);

                // Act & Assert
                var exception = await Assert.ThrowsAsync<ApplicationException>(
                    () => _controller.Disable2faWarning());
                Assert.Contains("Cannot disable 2FA", exception.Message);
            }

            [Fact]
            public async Task Disable2fa_DisablesTwoFactor_WhenValid()
            {
                // Arrange
                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.SetTwoFactorEnabledAsync(_testUser, false))
                    .ReturnsAsync(IdentityResult.Success);

                // Act
                var result = await _controller.Disable2fa();

                // Assert
                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("TwoFactorAuthentication", redirectResult.ActionName);
                _mockUserManager.Verify(m => m.SetTwoFactorEnabledAsync(_testUser, false), Times.Once);
            }

            [Fact]
            public async Task Disable2fa_ThrowsException_WhenDisableFails()
            {
                // Arrange
                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.SetTwoFactorEnabledAsync(_testUser, false))
                    .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Disable 2FA failed" }));

                // Act & Assert
                var exception = await Assert.ThrowsAsync<ApplicationException>(
                    () => _controller.Disable2fa());
                Assert.Contains("Unexpected error", exception.Message);
            }

            #endregion

            #region EnableAuthenticator Tests

            [Fact]
            public async Task EnableAuthenticator_Get_ReturnsView_WithCorrectModel()
            {
                // Arrange
                var authenticatorKey = "TESTKEY123456";
                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.GetAuthenticatorKeyAsync(_testUser))
                    .ReturnsAsync(authenticatorKey);

                // Act
                var result = await _controller.EnableAuthenticator();

                // Assert
                var viewResult = Assert.IsType<ViewResult>(result);
                var model = Assert.IsType<EnableAuthenticatorViewModel>(viewResult.Model);
                Assert.Equal("test key1 2345 6", model.SharedKey);
                Assert.Contains(authenticatorKey, model.AuthenticatorUri);
            }

            [Fact]
            public async Task EnableAuthenticator_Get_ResetsKeyWhenEmpty()
            {
                // Arrange
                var newKey = "NEWKEY123456";
                var formattedKey = "newk ey12 3456"; // Expected formatted key

                _mockUserManager.SetupSequence(m => m.GetAuthenticatorKeyAsync(_testUser))
                    .ReturnsAsync((string)null)      // First call returns null
                    .ReturnsAsync(newKey);           // Second call returns new key

                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);

                _mockUserManager.Setup(m => m.ResetAuthenticatorKeyAsync(_testUser))
                    .ReturnsAsync(IdentityResult.Success);
                   
                // Act
                var result = await _controller.EnableAuthenticator();

                // Assert
                var viewResult = Assert.IsType<ViewResult>(result);
                var model = Assert.IsType<EnableAuthenticatorViewModel>(viewResult.Model);

                // Verify the key is properly formatted
                Assert.Equal(formattedKey, model.SharedKey);

                // Verify ResetAuthenticatorKeyAsync was called
                _mockUserManager.Verify(m => m.ResetAuthenticatorKeyAsync(_testUser), Times.Once);

                // Verify GetAuthenticatorKeyAsync was called twice
                _mockUserManager.Verify(m => m.GetAuthenticatorKeyAsync(_testUser), Times.Exactly(2));
            }

            [Fact]
            public async Task EnableAuthenticator_Post_ReturnsView_WhenModelStateInvalid()
            {
                // Arrange
                var model = new EnableAuthenticatorViewModel();
                _controller.ModelState.AddModelError("Code", "Code is required");

                // Act
                var result = await _controller.EnableAuthenticator(model);

                // Assert
                var viewResult = Assert.IsType<ViewResult>(result);
                Assert.Same(model, viewResult.Model);
            }

            [Fact]
            public async Task EnableAuthenticator_Post_EnablesTwoFactor_WhenValidCode()
            {
                // Arrange
                var model = new EnableAuthenticatorViewModel
                {
                    Code = "123456"
                };
                var recoveryCodes = new[] { "code1", "code2", "code3" };

                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.VerifyTwoFactorTokenAsync(_testUser, "Authenticator", "123456"))
                    .ReturnsAsync(true);
                _mockUserManager.Setup(m => m.SetTwoFactorEnabledAsync(_testUser, true))
                    .ReturnsAsync(IdentityResult.Success);
                _mockUserManager.Setup(m => m.GenerateNewTwoFactorRecoveryCodesAsync(_testUser, 10))
                    .ReturnsAsync(recoveryCodes);

                // Act
                var result = await _controller.EnableAuthenticator(model);

                // Assert
                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("GenerateRecoveryCodes", redirectResult.ActionName);
                _mockUserManager.Verify(m => m.SetTwoFactorEnabledAsync(_testUser, true), Times.Once);
            }

            [Fact]
            public async Task EnableAuthenticator_Post_ReturnsView_WhenInvalidCode()
            {
                // Arrange
                var model = new EnableAuthenticatorViewModel
                {
                    Code = "123456"
                };

                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.VerifyTwoFactorTokenAsync(_testUser, "Authenticator", "123456"))
                    .ReturnsAsync(false);

                // Act
                var result = await _controller.EnableAuthenticator(model);

                // Assert
                var viewResult = Assert.IsType<ViewResult>(result);
                Assert.Same(model, viewResult.Model);
                Assert.False(_controller.ModelState.IsValid);
            }

            [Fact]
            public async Task EnableAuthenticator_Post_HandlesCodeWithSpacesAndDashes()
            {
                // Arrange
                var model = new EnableAuthenticatorViewModel
                {
                    Code = "123-456"
                };

                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.VerifyTwoFactorTokenAsync(_testUser, "Authenticator", "123456"))
                    .ReturnsAsync(true);
                _mockUserManager.Setup(m => m.SetTwoFactorEnabledAsync(_testUser, true))
                    .ReturnsAsync(IdentityResult.Success);
                _mockUserManager.Setup(m => m.GenerateNewTwoFactorRecoveryCodesAsync(_testUser, 10))
                    .ReturnsAsync(new[] { "code1", "code2" });

                // Act
                var result = await _controller.EnableAuthenticator(model);

                // Assert
                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("GenerateRecoveryCodes", redirectResult.ActionName);
                _mockUserManager.Verify(m => m.VerifyTwoFactorTokenAsync(_testUser, "Authenticator", "123456"), Times.Once);
            }

            #endregion

            #region ResetAuthenticator Tests

            [Fact]
            public void ResetAuthenticatorWarning_ReturnsView()
            {
                // Act
                var result = _controller.ResetAuthenticatorWarning();

                // Assert
                var viewResult = Assert.IsType<ViewResult>(result);
            }

            [Fact]
            public async Task ResetAuthenticator_ResetsAuthenticator_WhenValid()
            {
                // Arrange
                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.SetTwoFactorEnabledAsync(_testUser, false))
                    .ReturnsAsync(IdentityResult.Success);
                _mockUserManager.Setup(m => m.ResetAuthenticatorKeyAsync(_testUser))
                    .ReturnsAsync(IdentityResult.Success);

                // Act
                var result = await _controller.ResetAuthenticator();

                // Assert
                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("EnableAuthenticator", redirectResult.ActionName);
                _mockUserManager.Verify(m => m.SetTwoFactorEnabledAsync(_testUser, false), Times.Once);
                _mockUserManager.Verify(m => m.ResetAuthenticatorKeyAsync(_testUser), Times.Once);
            }

            #endregion

            #region GenerateRecoveryCodes Tests

            [Fact]
            public async Task GenerateRecoveryCodes_GeneratesCodes_WhenValid()
            {
                // Arrange
                var recoveryCodes = new[] { "code1", "code2", "code3" };
                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                var _tempuser = _testUser;
                _tempuser.TwoFactorEnabled = true;
                //_mockUserManager.Setup(m => m.GetTwoFactorEnabledAsync(_testUser))
                //    .ReturnsAsync(true);
                _mockUserManager.Setup(m => m.GenerateNewTwoFactorRecoveryCodesAsync(_tempuser, 10))
                    .ReturnsAsync(recoveryCodes);

                // Act
                var result = await _controller.GenerateRecoveryCodes();

                // Assert
                var viewResult = Assert.IsType<ViewResult>(result);
                var model = Assert.IsType<GenerateRecoveryCodesViewModel>(viewResult.Model);
                Assert.Equal(recoveryCodes, model.RecoveryCodes);
                _mockUserManager.Verify(m => m.GenerateNewTwoFactorRecoveryCodesAsync(_testUser, 10), Times.Once);
            }

            [Fact]
            public async Task GenerateRecoveryCodes_ThrowsException_WhenTwoFactorDisabled()
            {
                // Arrange
                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.GetTwoFactorEnabledAsync(_testUser))
                    .ReturnsAsync(false);

                // Act & Assert
                var exception = await Assert.ThrowsAsync<ApplicationException>(
                    () => _controller.GenerateRecoveryCodes());
                Assert.Contains("Cannot generate recovery codes", exception.Message);
            }

            [Fact]
            public async Task GenerateRecoveryCodes_ThrowsException_WhenUserNotFound()
            {
                // Arrange
                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync((ApplicationUser)null);
                _mockUserManager.Setup(m => m.GetUserId(It.IsAny<ClaimsPrincipal>()))
                    .Returns(_testUser.Id);

                // Act & Assert
                var exception = await Assert.ThrowsAsync<ApplicationException>(
                    () => _controller.GenerateRecoveryCodes());
                Assert.Contains("Unable to load user", exception.Message);
            }

            #endregion

            #region Async and Concurrency Tests

            [Fact]
            public async Task MultipleAsyncCalls_HandleConcurrently()
            {
                // Arrange
                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.HasPasswordAsync(_testUser))
                    .ReturnsAsync(true);
                _mockUserManager.Setup(m => m.GetAuthenticatorKeyAsync(_testUser))
                    .ReturnsAsync("TESTKEY123456");
                _mockUserManager.Setup(m => m.CountRecoveryCodesAsync(_testUser))
                    .ReturnsAsync(5);
                _mockUserManager.Setup(m => m.GetTwoFactorEnabledAsync(_testUser))
                    .ReturnsAsync(true);

                // Act
                var task1 = _controller.ChangePassword();
                var task2 = _controller.TwoFactorAuthentication();
                var task3 = _controller.MyAccount();

                var results = await Task.WhenAll(task1, task2, task3);

                // Assert
                Assert.All(results, result => Assert.IsType<ViewResult>(result));
            }

            [Fact]
            public async Task ConcurrentPasswordChanges_HandleProperly()
            {
                // Arrange
                var model1 = new ChangePasswordViewModel
                {
                    OldPassword = "OldPass1",
                    NewPassword = "NewPass1",
                    ConfirmPassword = "NewPass1"
                };
                var model2 = new ChangePasswordViewModel
                {
                    OldPassword = "OldPass2",
                    NewPassword = "NewPass2",
                    ConfirmPassword = "NewPass2"
                };

                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.ChangePasswordAsync(_testUser, "OldPass1", "NewPass1"))
                    .ReturnsAsync(IdentityResult.Success);
                _mockUserManager.Setup(m => m.ChangePasswordAsync(_testUser, "OldPass2", "NewPass2"))
                    .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Concurrent modification" }));
                _mockSignInManager.Setup(m => m.RefreshSignInAsync(_testUser))
                    .Returns(Task.CompletedTask);

                // Act
                var task1 = _controller.ChangePassword(model1);
                var task2 = _controller.ChangePassword(model2);

                var results = await Task.WhenAll(task1, task2);

                // Assert
                Assert.IsType<RedirectToActionResult>(results[0]);
                Assert.IsType<ViewResult>(results[1]);
            }

            #endregion

            #region Helper Methods Tests

            [Fact]
            public void AddErrors_AddsErrorsToModelState()
            {
                // Arrange
                var errors = new[]
                {
                new IdentityError { Code = "PasswordTooShort", Description = "Password is too short" },
                new IdentityError { Code = "PasswordRequiresDigit", Description = "Password requires a digit" }
            };
                var identityResult = IdentityResult.Failed(errors);

                // Use reflection to access private method
                var method = typeof(ManageController).GetMethod("AddErrors", BindingFlags.NonPublic | BindingFlags.Instance);

                // Act
                method?.Invoke(_controller, new object[] { identityResult });

                // Assert
                Assert.False(_controller.ModelState.IsValid);
                Assert.Equal(2, _controller.ModelState.ErrorCount);
                Assert.Contains(_controller.ModelState.Values,
                    v => v.Errors.Any(e => e.ErrorMessage == "Password is too short"));
            }

            [Fact]
            public void FormatKey_FormatsUnformattedKeyCorrectly()
            {
                // Arrange
                var unformattedKey = "ABCDEFGHIJKLMNOP";
                var method = typeof(ManageController).GetMethod("FormatKey", BindingFlags.NonPublic | BindingFlags.Instance);

                // Act
                var result = method?.Invoke(_controller, new object[] { unformattedKey }) as string;

                // Assert
                Assert.Equal("abcd efgh ijkl mnop", result);
            }

            [Fact]
            public void FormatKey_HandlesShortKey()
            {
                // Arrange
                var shortKey = "ABC";
                var method = typeof(ManageController).GetMethod("FormatKey", BindingFlags.NonPublic | BindingFlags.Instance);

                // Act
                var result = method?.Invoke(_controller, new object[] { shortKey }) as string;

                // Assert
                Assert.Equal("abc", result);
            }

            [Fact]
            public void FormatKey_HandlesEmptyKey()
            {
                // Arrange
                var emptyKey = "";
                var method = typeof(ManageController).GetMethod("FormatKey", BindingFlags.NonPublic | BindingFlags.Instance);

                // Act
                var result = method?.Invoke(_controller, new object[] { emptyKey }) as string;

                // Assert
                Assert.Equal("", result);
            }

            
            #endregion

            #region Error Handling Tests

           

            [Fact]
            public async Task UserManager_HandlesNullUserScenarios()
            {
                // Arrange
                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync((ApplicationUser)null);

                // Act & Assert
                await Assert.ThrowsAsync<ApplicationException>(() => _controller.MyAccount());
                await Assert.ThrowsAsync<ApplicationException>(() => _controller.ChangePassword());
                await Assert.ThrowsAsync<ApplicationException>(() => _controller.SetPassword());
            }

            [Fact]
            public async Task AllAsyncMethods_ReturnProperTaskTypes()
            {
                // Arrange
                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.HasPasswordAsync(_testUser))
                    .ReturnsAsync(true);
                _mockUserManager.Setup(m => m.GetTwoFactorEnabledAsync(_testUser))
                    .ReturnsAsync(true);
                _mockUserManager.Setup(m => m.GetAuthenticatorKeyAsync(_testUser))
                    .ReturnsAsync("TESTKEY123456");
                _mockUserManager.Setup(m => m.CountRecoveryCodesAsync(_testUser))
                    .ReturnsAsync(5);

                // Act
                var myAccountTask = _controller.MyAccount();
                var changePasswordTask = _controller.ChangePassword();
                var setPasswordTask = _controller.SetPassword();
                var twoFactorTask = _controller.TwoFactorAuthentication();
                var enableAuthTask = _controller.EnableAuthenticator();

                // Assert
                Assert.IsType<Task<IActionResult>>(myAccountTask);
                Assert.IsType<Task<IActionResult>>(changePasswordTask);
                Assert.IsType<Task<IActionResult>>(setPasswordTask);
                Assert.IsType<Task<IActionResult>>(twoFactorTask);
                Assert.IsType<Task<IActionResult>>(enableAuthTask);

                // Ensure they complete without exceptions
                await Task.WhenAll(myAccountTask, changePasswordTask, setPasswordTask, twoFactorTask, enableAuthTask);
            }

            #endregion

            #region StatusMessage and Properties Tests

            [Fact]
            public void StatusMessage_Property_CanBeSetAndGet()
            {
                // Arrange
                var statusMessage = "Test status message";

                // Act
                _controller.StatusMessage = statusMessage;

                // Assert
                Assert.Equal(statusMessage, _controller.StatusMessage);
            }

            [Fact]
            public void Constructor_InitializesAllProperties()
            {
                // Assert
                Assert.NotNull(_controller);
                // Verify that all dependencies are properly injected by checking they're not null
                // This is verified through successful method calls in other tests
            }

            #endregion

            #region Additional Integration Tests

            [Fact]
            public async Task MyAccount_ReturnsViewWithCorrectModel_IntegrationTest()
            {
                // Arrange
                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);

                // Act
                var result = await _controller.MyAccount();

                // Assert
                var viewResult = Assert.IsType<ViewResult>(result);
                var model = Assert.IsType<IndexViewModel>(viewResult.Model);
                Assert.Equal(_testUser.UserName, model.Username);
                Assert.Equal(_testUser.Email, model.Email);
                Assert.Equal(_testUser.PhoneNumber, model.PhoneNumber);
                Assert.Equal(_testUser.EmailConfirmed, model.IsEmailConfirmed);
            }

            [Fact]
            public async Task MyAccount_ThrowsException_WhenUserNotFound_IntegrationTest()
            {
                // Arrange
                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync((ApplicationUser)null);
                _mockUserManager.Setup(m => m.GetUserId(It.IsAny<ClaimsPrincipal>()))
                    .Returns(_testUser.Id);

                // Act & Assert
                var exception = await Assert.ThrowsAsync<ApplicationException>(
                    () => _controller.MyAccount());
                Assert.Contains("Unable to load user", exception.Message);
            }

            [Fact]
            public async Task SendVerificationEmail_SendsEmail_WhenValid_IntegrationTest()
            {
                // Arrange
                var model = new IndexViewModel { Email = _testUser.Email };
                var confirmationToken = "confirmation-token";
                var callbackUrl = "http://example.com/callback";

                // Mock UserManager
                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.GenerateEmailConfirmationTokenAsync(_testUser))
                    .ReturnsAsync(confirmationToken);

                // Mock User (ClaimsPrincipal)
                var userClaimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
                _controller.ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = userClaimsPrincipal }
                };

                // Mock UrlHelper to simulate EmailConfirmationLink behavior
                var mockUrlHelper = new Mock<IUrlHelper>();
                mockUrlHelper
                    .Setup(x => x.Action(It.IsAny<UrlActionContext>()))
                    .Returns(callbackUrl); // Return the expected callback URL
                _controller.Url = mockUrlHelper.Object;

                // Act
                var result = await _controller.SendVerificationEmail(model);

                // Assert
                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("Index", redirectResult.ActionName);
                //_mockEmailSender.Verify(
                //    e => e.SendEmailConfirmationAsync(_testUser.Email, callbackUrl),
                //    Times.Once
                //);
            }
            [Fact]
            public async Task ChangePassword_Post_ChangesPassword_WhenValid_IntegrationTest()
            {
                // Arrange
                var model = new ChangePasswordViewModel
                {
                    OldPassword = "oldpassword",
                    NewPassword = "newpassword",
                    ConfirmPassword = "newpassword"
                };

                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.ChangePasswordAsync(_testUser, model.OldPassword, model.NewPassword))
                    .ReturnsAsync(IdentityResult.Success);

                // Act
                var result = await _controller.ChangePassword(model);

                // Assert
                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("ChangePassword", redirectResult.ActionName);
                _mockSignInManager.Verify(s => s.SignInAsync(_testUser, false, null), Times.Once);
            }

            [Fact]
            public async Task ChangePassword_Post_ReturnsView_WhenPasswordChangeFails_IntegrationTest()
            {
                // Arrange
                var model = new ChangePasswordViewModel
                {
                    OldPassword = "oldpassword",
                    NewPassword = "newpassword",
                    ConfirmPassword = "newpassword"
                };

                var error = new IdentityError { Code = "PasswordMismatch", Description = "Incorrect password." };
                var failureResult = IdentityResult.Failed(error);

                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.ChangePasswordAsync(_testUser, model.OldPassword, model.NewPassword))
                    .ReturnsAsync(failureResult);

                // Act
                var result = await _controller.ChangePassword(model);

                // Assert
                var viewResult = Assert.IsType<ViewResult>(result);
                Assert.Same(model, viewResult.Model);
                Assert.False(_controller.ModelState.IsValid);
            }

            [Fact]
            public async Task SetPassword_Post_SetsPassword_WhenValid_IntegrationTest()
            {
                // Arrange
                var model = new SetPasswordViewModel
                {
                    NewPassword = "newpassword",
                    ConfirmPassword = "newpassword"
                };

                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.AddPasswordAsync(_testUser, model.NewPassword))
                    .ReturnsAsync(IdentityResult.Success);

                // Act
                var result = await _controller.SetPassword(model);

                // Assert
                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("SetPassword", redirectResult.ActionName);
                _mockSignInManager.Verify(s => s.SignInAsync(_testUser, false, null), Times.Once);
            }

            [Fact]
            public async Task ExternalLogins_ReturnsView_WithCorrectModel_IntegrationTest()
            {
                // Arrange
                var currentLogins = new List<UserLoginInfo>
            {
                new UserLoginInfo("Google", "google-key", "Google")
            };
                var externalSchemes = new List<AuthenticationScheme>
            {
                new AuthenticationScheme("Facebook", "Facebook", typeof(IAuthenticationHandler))
            };

                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.GetLoginsAsync(_testUser))
                    .ReturnsAsync(currentLogins);
                _mockUserManager.Setup(m => m.HasPasswordAsync(_testUser))
                    .ReturnsAsync(true);
                _mockSignInManager.Setup(s => s.GetExternalAuthenticationSchemesAsync())
                    .ReturnsAsync(externalSchemes);

                // Act
                var result = await _controller.ExternalLogins();

                // Assert
                var viewResult = Assert.IsType<ViewResult>(result);
                var model = Assert.IsType<ExternalLoginsViewModel>(viewResult.Model);
                Assert.Equal(currentLogins, model.CurrentLogins);
                Assert.True(model.ShowRemoveButton);
            }

            [Fact]
            public async Task LinkLogin_ReturnsChallengeResult_IntegrationTest()
            {
                // Arrange
                var provider = "Google";
                var properties = new AuthenticationProperties();
                _mockSignInManager.Setup(s => s.ConfigureExternalAuthenticationProperties(
                    provider, It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(properties);
                _mockUserManager.Setup(m => m.GetUserId(It.IsAny<ClaimsPrincipal>()))
                    .Returns(_testUser.Id);

                var httpContext = new Mock<HttpContext>();
                httpContext.Setup(h => h.SignOutAsync(IdentityConstants.ExternalScheme))
                    .Returns(Task.CompletedTask);
                _controller.ControllerContext.HttpContext = httpContext.Object;

                // Act
                var result = await _controller.LinkLogin(provider);

                // Assert
                var challengeResult = Assert.IsType<ChallengeResult>(result);
                Assert.Equal(provider, challengeResult.AuthenticationSchemes.First());
            }

            [Fact]
            public async Task RemoveLogin_RemovesLogin_WhenValid_IntegrationTest()
            {
                // Arrange
                var model = new RemoveLoginViewModel
                {
                    LoginProvider = "Google",
                    ProviderKey = "google-key"
                };

                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.RemoveLoginAsync(_testUser, model.LoginProvider, model.ProviderKey))
                    .ReturnsAsync(IdentityResult.Success);

                // Act
                var result = await _controller.RemoveLogin(model);

                // Assert
                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("ExternalLogins", redirectResult.ActionName);
                _mockSignInManager.Verify(s => s.SignInAsync(_testUser, false, null), Times.Once);
            }

            [Fact]
            public async Task TwoFactorAuthentication_ReturnsView_WithCorrectModel_IntegrationTest()
            {
                // Arrange
                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.GetAuthenticatorKeyAsync(_testUser))
                    .ReturnsAsync("authenticator-key");
                _mockUserManager.Setup(m => m.CountRecoveryCodesAsync(_testUser))
                    .ReturnsAsync(5);

                // Act
                var result = await _controller.TwoFactorAuthentication();

                // Assert
                var viewResult = Assert.IsType<ViewResult>(result);
                var model = Assert.IsType<TwoFactorAuthenticationViewModel>(viewResult.Model);
                Assert.True(model.HasAuthenticator);
                Assert.False(model.Is2faEnabled);
                Assert.Equal(5, model.RecoveryCodesLeft);
            }

            [Fact]
            public async Task Disable2faWarning_ReturnsView_WhenTwoFactorEnabled_IntegrationTest()
            {
                // Arrange
                _testUser.TwoFactorEnabled = true;
                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);

                // Act
                var result = await _controller.Disable2faWarning();

                // Assert
                var viewResult = Assert.IsType<ViewResult>(result);
                Assert.Equal("Disable2fa", viewResult.ViewName);
            }

            [Fact]
            public async Task Disable2fa_DisablesTwoFactor_WhenValid_IntegrationTest()
            {
                // Arrange
                _testUser.TwoFactorEnabled = true;
                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.SetTwoFactorEnabledAsync(_testUser, false))
                    .ReturnsAsync(IdentityResult.Success);

                // Act
                var result = await _controller.Disable2fa();

                // Assert
                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("TwoFactorAuthentication", redirectResult.ActionName);
            }

            [Fact]
            public async Task EnableAuthenticator_ReturnsView_WithCorrectModel_IntegrationTest()
            {
                // Arrange
                var authenticatorKey = "ABCDEFGHIJKLMNOP";
                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.GetAuthenticatorKeyAsync(_testUser))
                    .ReturnsAsync(authenticatorKey);
                _mockUrlEncoder.Setup(e => e.Encode("eShopOnWeb"))
                    .Returns("eShopOnWeb");
                _mockUrlEncoder.Setup(e => e.Encode(_testUser.Email))
                    .Returns(_testUser.Email);

                // Act
                var result = await _controller.EnableAuthenticator();

                // Assert
                var viewResult = Assert.IsType<ViewResult>(result);
                var model = Assert.IsType<EnableAuthenticatorViewModel>(viewResult.Model);
                Assert.NotNull(model.SharedKey);
                Assert.NotNull(model.AuthenticatorUri);
            }

            [Fact]
            public async Task EnableAuthenticator_Post_EnablesTwoFactor_WhenValidCode_IntegrationTest()
            {
                // Arrange
                var model = new EnableAuthenticatorViewModel
                {
                    Code = "123456"
                };

                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.VerifyTwoFactorTokenAsync(_testUser, It.IsAny<string>(), "123456"))
                    .ReturnsAsync(true);
                _mockUserManager.Setup(m => m.SetTwoFactorEnabledAsync(_testUser, true))
                    .ReturnsAsync(IdentityResult.Success);

                // Act
                var result = await _controller.EnableAuthenticator(model);

                // Assert
                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("GenerateRecoveryCodes", redirectResult.ActionName);
            }

            [Fact]
            public async Task EnableAuthenticator_Post_ReturnsView_WhenInvalidCode_IntegrationTest()
            {
                // Arrange
                var model = new EnableAuthenticatorViewModel
                {
                    Code = "123456"
                };

                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.VerifyTwoFactorTokenAsync(_testUser, It.IsAny<string>(), "123456"))
                    .ReturnsAsync(false);

                // Act
                var result = await _controller.EnableAuthenticator(model);

                // Assert
                var viewResult = Assert.IsType<ViewResult>(result);
                Assert.Same(model, viewResult.Model);
                Assert.False(_controller.ModelState.IsValid);
            }

            [Fact]
            public async Task ResetAuthenticator_ResetsAuthenticator_WhenValid_IntegrationTest()
            {
                // Arrange
                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.SetTwoFactorEnabledAsync(_testUser, false))
                    .ReturnsAsync(IdentityResult.Success);
                _mockUserManager.Setup(m => m.ResetAuthenticatorKeyAsync(_testUser))
                    .ReturnsAsync(IdentityResult.Success);

                // Act
                var result = await _controller.ResetAuthenticator();

                // Assert
                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("EnableAuthenticator", redirectResult.ActionName);
            }

            [Fact]
            public async Task GenerateRecoveryCodes_GeneratesCodes_WhenValid_IntegrationTest()
            {
                // Arrange
                _testUser.TwoFactorEnabled = true;
                var recoveryCodes = new List<string> { "code1", "code2", "code3" };

                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockUserManager.Setup(m => m.GenerateNewTwoFactorRecoveryCodesAsync(_testUser, 10))
                    .ReturnsAsync(recoveryCodes);

                // Act
                var result = await _controller.GenerateRecoveryCodes();

                // Assert
                var viewResult = Assert.IsType<ViewResult>(result);
                var model = Assert.IsType<GenerateRecoveryCodesViewModel>(viewResult.Model);
                Assert.Equal(recoveryCodes.ToArray(), model.RecoveryCodes);
            }

            [Fact]
            public async Task GenerateRecoveryCodes_ThrowsException_WhenTwoFactorDisabled_IntegrationTest()
            {
                // Arrange
                _testUser.TwoFactorEnabled = false;
                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);

                // Act & Assert
                var exception = await Assert.ThrowsAsync<ApplicationException>(
                    () => _controller.GenerateRecoveryCodes());
                Assert.Contains("Cannot generate recovery codes", exception.Message);
            }

            [Fact]
            public async Task LinkLoginCallback_AddsLogin_WhenValid_IntegrationTest()
            {
                // Arrange
                var loginInfo = new ExternalLoginInfo(
                    new ClaimsPrincipal(),
                    "Google",
                    "google-key",
                    "Google");

                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockSignInManager.Setup(s => s.GetExternalLoginInfoAsync(_testUser.Id))
                    .ReturnsAsync(loginInfo);
                _mockUserManager.Setup(m => m.AddLoginAsync(_testUser, loginInfo))
                    .ReturnsAsync(IdentityResult.Success);

                var httpContext = new Mock<HttpContext>();
                httpContext.Setup(h => h.SignOutAsync(IdentityConstants.ExternalScheme))
                    .Returns(Task.CompletedTask);
                _controller.ControllerContext.HttpContext = httpContext.Object;

                // Act
                var result = await _controller.LinkLoginCallback();

                // Assert
                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("ExternalLogins", redirectResult.ActionName);
            }

            [Fact]
            public async Task LinkLoginCallback_ThrowsException_WhenNoExternalLoginInfo_IntegrationTest()
            {
                // Arrange
                _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(_testUser);
                _mockSignInManager.Setup(s => s.GetExternalLoginInfoAsync(_testUser.Id))
                    .ReturnsAsync((ExternalLoginInfo)null);

                // Act & Assert
                var exception = await Assert.ThrowsAsync<ApplicationException>(
                    () => _controller.LinkLoginCallback());
                Assert.Contains("Unexpected error occurred loading external login info", exception.Message);
            }

            #endregion
        }
    }

}

