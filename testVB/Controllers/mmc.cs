'using Microsoft.AspNetCore.Authentication;
'using Microsoft.AspNetCore.Http;
'using Microsoft.AspNetCore.Identity;
'using Microsoft.AspNetCore.Mvc;
'using Microsoft.AspNetCore.Mvc.Routing;
'using Microsoft.eShopWeb.ApplicationCore.Interfaces;
'using Microsoft.eShopWeb.Infrastructure.Identity;
'using Microsoft.eShopWeb.Web.Controllers;
'using Microsoft.eShopWeb.Web.Services;
'using Microsoft.eShopWeb.Web.ViewModels.Manage;
'using Moq;
'using System;
'using System.Collections.Generic;
'using System.Net;
'using System.Security.Claims;
'using System.Text.Encodings.Web;
'using System.Threading.Tasks;
'using Xunit;

'namespace Microsoft.eShopWeb.Web.Tests.Controllers
'{
'    public class ManageControllerTests
'    {
'        private const string TestUserId = "test-user-id";
'        private const string TestUserName = "testuser@example.com";
'        private const string TestEmail = "testuser@example.com";
'        private const string TestPhoneNumber = "123-456-7890";

'        private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
'        private readonly Mock<SignInManager<ApplicationUser>> _mockSignInManager;
'        private readonly Mock<IEmailSender> _mockEmailSender;
'        private readonly Mock<IAppLogger<ManageController>> _mockLogger;
'        private readonly Mock<UrlEncoder> _mockUrlEncoder;
'        private readonly ManageController _controller;

'        public ManageControllerTests()
'        {
'            _mockUserManager = new Mock<UserManager<ApplicationUser>>(
'                Mock.Of<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);

'            _mockSignInManager = new Mock<SignInManager<ApplicationUser>>(
'                _mockUserManager.Object,
'                Mock.Of<IHttpContextAccessor>(),
'                Mock.Of<IUserClaimsPrincipalFactory<ApplicationUser>>(),
'                null, null, null, null);

'            _mockEmailSender = new Mock<IEmailSender>();
'            _mockLogger = new Mock<IAppLogger<ManageController>>();
'            _mockUrlEncoder = new Mock<UrlEncoder>();

'            _controller = new ManageController(
'                _mockUserManager.Object,
'                _mockSignInManager.Object,
'                _mockEmailSender.Object,
'                _mockLogger.Object,
'                _mockUrlEncoder.Object);

'            SetupControllerContext();
'        }

'        private void SetupControllerContext()
'        {
'            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
'            {
'                new Claim(ClaimTypes.NameIdentifier, TestUserId),
'                new Claim(ClaimTypes.Name, TestUserName),
'                new Claim(ClaimTypes.Email, TestEmail)
'            }, "mock"));

'            _controller.ControllerContext = new ControllerContext()
'            {
'                HttpContext = new DefaultHttpContext() { User = user }
'            };
'        }

'        #region MyAccount Action Tests
'        [Fact]
'        public async Task MyAccount_WhenUserExists_ReturnsViewWithUserDetails()
'        {
'            // Arrange
'            var testUser = new ApplicationUser
'            {
'                UserName = "testuser",
'                Email = "test@example.com",
'                PhoneNumber = "123-456-7890",
'                EmailConfirmed = true
'            };

'            _mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
'                .ReturnsAsync(testUser);

'            // Act
'            var result = await _controller.MyAccount();

'            // Assert
'            var viewResult = Assert.IsType<ViewResult>(result);
'            var model = Assert.IsType<IndexViewModel>(viewResult.Model);

'            Assert.Equal(testUser.UserName, model.Username);
'            Assert.Equal(testUser.Email, model.Email);
'            Assert.Equal(testUser.PhoneNumber, model.PhoneNumber);
'            Assert.True(model.IsEmailConfirmed);
'        }

'        [Fact]
'        public async Task MyAccount_WhenUserNotFound_ThrowsUserNotFoundException()
'        {
'            // Arrange
'            _mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
'                .ReturnsAsync((ApplicationUser)null);
'            _mockUserManager.Setup(x => x.GetUserId(It.IsAny<ClaimsPrincipal>()))
'                .Returns(TestUserId);
'            // Act & Assert
'            var ex = await Assert.ThrowsAsync<ApplicationException>(() => _controller.MyAccount());
'            Assert.Contains($"Unable to load user with ID '{TestUserId}'", ex.Message);
'        }
'        #endregion

'        #region Index (POST) Action Tests
'        [Fact]
'        public async Task IndexPost_InvalidModel_ReturnsViewWithModel()
'        {
'            // Arrange
'            _controller.ModelState.AddModelError("Email", "Email is required");
'            var model = new IndexViewModel();

'            // Act
'            var result = await _controller.Index(model);

'            // Assert
'            var viewResult = Assert.IsType<ViewResult>(result);
'            Assert.Same(model, viewResult.Model);
'            Assert.False(_controller.ModelState.IsValid);
'        }

'        [Fact]
'        public async Task IndexPost_UserNotFound_ThrowsCorrectException()
'        {
'            // Arrange
'            _mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
'                .ReturnsAsync((ApplicationUser)null);
'            var model = new IndexViewModel();

'            // Act & Assert
'            var ex = await Assert.ThrowsAsync<ApplicationException>(() => _controller.Index(model));
'            Assert.Contains("Unable to load user with ID", ex.Message);
'        }

'        [Fact]
'        public async Task IndexPost_EmailUpdateFails_ThrowsCorrectException()
'        {
'            // Arrange
'            var testUser = new ApplicationUser { Id = "user1", Email = "old@test.com" };
'            var model = new IndexViewModel { Email = "new@test.com" };

'            _mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
'                .ReturnsAsync(testUser);
'            _mockUserManager.Setup(x => x.SetEmailAsync(testUser, model.Email))
'                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Email error" }));
            
'            // Act & Assert
'            var ex = await Assert.ThrowsAsync<ApplicationException>(() => _controller.Index(model));
'            Assert.Contains("Unexpected error occurred setting email for user with ID 'user1'", ex.Message);
'        }

'        [Fact]
'        public async Task IndexPost_PhoneUpdateFails_ThrowsCorrectException()
'        {
'            // Arrange
'            var testUser = new ApplicationUser { Id = "user1", PhoneNumber = "old-phone" };
'            var model = new IndexViewModel { PhoneNumber = "new-phone" };

'            _mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
'                .ReturnsAsync(testUser);
'            _mockUserManager.Setup(x => x.SetPhoneNumberAsync(testUser, model.PhoneNumber))
'                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Phone error" }));

'            // Act & Assert
'            var ex = await Assert.ThrowsAsync<ApplicationException>(() => _controller.Index(model));
'            Assert.Contains("Unexpected error occurred setting phone number for user with ID 'user1'", ex.Message);
'        }

'        [Fact]
'        public async Task IndexPost_ValidUpdate_SetsStatusAndRedirects()
'        {
'            // Arrange
'            var testUser = new ApplicationUser { Email = "test@test.com", PhoneNumber = "123-456" };
'            var model = new IndexViewModel { Email = "test@test.com", PhoneNumber = "123-456" };

'            _mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
'                .ReturnsAsync(testUser);

'            // Act
'            var result = await _controller.Index(model);

'            // Assert
'            var redirect = Assert.IsType<RedirectToActionResult>(result);
'            Assert.Equal("Index", redirect.ActionName);
'            Assert.Equal("Your profile has been updated", _controller.StatusMessage);
'        }


'        #endregion
'        [Fact]
'        public async Task SendVerificationEmail_ValidRequest_SendsConfirmationEmail()
'        {
'            // Arrange
'            var testUser = new ApplicationUser
'            {
'                Id = "user123",
'                Email = "test@example.com"
'            };
'            var model = new IndexViewModel();
'            var expectedCode = "confirmation-code";
'            var expectedUrl = "https://example.com/Account/ConfirmEmail?userId=user123&code=confirmation-code";
'            var expectedBody = $"Please confirm your account by clicking this link: <a href='{WebUtility.HtmlEncode(expectedUrl)}'>link</a>";

'            _mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
'                .ReturnsAsync(testUser);
'            _mockUserManager.Setup(x => x.GenerateEmailConfirmationTokenAsync(testUser))
'                .ReturnsAsync(expectedCode);

'            var mockUrlHelper = new Mock<IUrlHelper>();
'            mockUrlHelper.Setup(x => x.Action(
'                It.Is<UrlActionContext>(u =>
'                    u.Action == "ConfirmEmail" &&
'                    u.Controller == "Account" &&
'                    u.Protocol == "https" &&
'                    (string)u.Values.GetType().GetProperty("userId").GetValue(u.Values) == testUser.Id &&
'                    (string)u.Values.GetType().GetProperty("code").GetValue(u.Values) == expectedCode
'                )))
'                .Returns(expectedUrl);
'            _controller.Url = mockUrlHelper.Object;
'            _controller.ControllerContext.HttpContext.Request.Scheme = "https";

'            // Act
'            var result = await _controller.SendVerificationEmail(model);

'            // Assert
'            _mockEmailSender.Verify(x => x.SendEmailAsync(
'                testUser.Email,
'                "Confirm your email",
'                expectedBody),  // Exact match of what VB.NET is sending
'                Times.Once);

'            Assert.Equal("Verification email sent. Please check your email.", _controller.StatusMessage);
'            Assert.IsType<RedirectToActionResult>(result);
'        }

'        [Fact]
'        public async Task SendVerificationEmail_InvalidModel_ReturnsViewWithModel()
'        {
'            // Arrange
'            var model = new IndexViewModel();
'            _controller.ModelState.AddModelError("Email", "Required");

'            // Act
'            var result = await _controller.SendVerificationEmail(model);

'            // Assert
'            var viewResult = Assert.IsType<ViewResult>(result);
'            Assert.Same(model, viewResult.Model);
'            _mockUserManager.Verify(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()), Times.Never);
'        }

'        [Fact]
'        public async Task SendVerificationEmail_UserNotFound_ThrowsException()
'        {
'            // Arrange
'            var model = new IndexViewModel();
'            _mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
'                .ReturnsAsync((ApplicationUser)null);

'            // Act & Assert
'            var ex = await Assert.ThrowsAsync<ApplicationException>(
'                () => _controller.SendVerificationEmail(model));

'            Assert.Contains("Unable to load user with ID", ex.Message);
'        }


'        #region ChangePassword Action Tests

'        [Fact]
'        public async Task ChangePassword_GET_WhenUserHasPassword_ReturnsViewWithModel()
'        {
'            // Arrange
'            var testUser = new ApplicationUser();
'            _mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
'                .ReturnsAsync(testUser);
'            _mockUserManager.Setup(x => x.HasPasswordAsync(testUser))
'                .ReturnsAsync(true);

'            // Act
'            var result = await _controller.ChangePassword();

'            // Assert
'            var viewResult = Assert.IsType<ViewResult>(result);
'            Assert.IsType<ChangePasswordViewModel>(viewResult.Model);
'        }
'        [Fact]
'        public async Task ChangePassword_GET_WhenUserHasNoPassword_RedirectsToSetPassword()
'        {
'            // Arrange
'            var testUser = new ApplicationUser();
'            _mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
'                .ReturnsAsync(testUser);
'            _mockUserManager.Setup(x => x.HasPasswordAsync(testUser))
'                .ReturnsAsync(false);

'            // Act
'            var result = await _controller.ChangePassword();

'            // Assert
'            var redirect = Assert.IsType<RedirectToActionResult>(result);
'            Assert.Equal("SetPassword", redirect.ActionName);
'        }
'        [Fact]
'        public async Task ChangePassword_GET_WhenUserNotFound_ThrowsException()
'        {
'            // Arrange
'            _mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
'                .ReturnsAsync((ApplicationUser)null);

'            // Act & Assert
'            await Assert.ThrowsAsync<ApplicationException>(() => _controller.ChangePassword());
'        }

'        [Fact]
'        public async Task ChangePassword_POST_ValidRequest_ChangesPasswordAndSignsIn()
'        {
'            // Arrange
'            var testUser = new ApplicationUser();
'            var model = new ChangePasswordViewModel
'            {
'                OldPassword = "oldPass",
'                NewPassword = "newPass"
'            };

'            _mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
'                .ReturnsAsync(testUser);
'            _mockUserManager.Setup(x => x.ChangePasswordAsync(testUser, model.OldPassword, model.NewPassword))
'                .ReturnsAsync(IdentityResult.Success);
'            _mockSignInManager.Setup(x => x.SignInAsync(testUser, false, null))
'                .Returns(Task.CompletedTask);

'            // Act
'            var result = await _controller.ChangePassword(model);

'            // Assert
'            _mockUserManager.Verify(x => x.ChangePasswordAsync(testUser, model.OldPassword, model.NewPassword), Times.Once);
'            _mockSignInManager.Verify(x => x.SignInAsync(testUser, false, null), Times.Once);
'            Assert.Equal("Your password has been changed.", _controller.StatusMessage);
'            Assert.IsType<RedirectToActionResult>(result);
'        }
'        [Fact]
'        public async Task ChangePassword_POST_InvalidModel_ReturnsViewWithModel()
'        {
'            // Arrange
'            var model = new ChangePasswordViewModel();
'            _controller.ModelState.AddModelError("Error", "Sample error");

'            // Act
'            var result = await _controller.ChangePassword(model);

'            // Assert
'            var viewResult = Assert.IsType<ViewResult>(result);
'            Assert.Same(model, viewResult.Model);
'        }
'        [Fact]
'        public async Task ChangePassword_POST_WhenChangeFails_ReturnsViewWithErrors()
'        {
'            // Arrange
'            var testUser = new ApplicationUser();
'            var model = new ChangePasswordViewModel
'            {
'                OldPassword = "oldPass",
'                NewPassword = "newPass"
'            };
'            var errors = new[] { new IdentityError { Description = "Error" } };

'            _mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
'                .ReturnsAsync(testUser);
'            _mockUserManager.Setup(x => x.ChangePasswordAsync(testUser, model.OldPassword, model.NewPassword))
'                .ReturnsAsync(IdentityResult.Failed(errors));

'            // Act
'            var result = await _controller.ChangePassword(model);

'            // Assert
'            var viewResult = Assert.IsType<ViewResult>(result);
'            Assert.False(_controller.ModelState.IsValid);
'            Assert.Contains("Error", _controller.ModelState[string.Empty].Errors[0].ErrorMessage);
'        }
'        [Fact]
'        public async Task ChangePassword_POST_WhenUserNotFound_ThrowsException()
'        {
'            // Arrange
'            var model = new ChangePasswordViewModel();
'            _mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
'                .ReturnsAsync((ApplicationUser)null);

'            // Act & Assert
'            await Assert.ThrowsAsync<ApplicationException>(() => _controller.ChangePassword(model));
'        }

'        #endregion

'        #region SetPassword Action Tests
'        [Fact]
'        public async Task SetPassword_GET_WhenUserHasPassword_RedirectsToChangePassword()
'        {
'            // Arrange
'            var testUser = new ApplicationUser();
'            _mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
'                .ReturnsAsync(testUser);
'            _mockUserManager.Setup(x => x.HasPasswordAsync(testUser))
'                .ReturnsAsync(true);

'            // Act
'            var result = await _controller.SetPassword();

'            // Assert
'            var redirect = Assert.IsType<RedirectToActionResult>(result);
'            Assert.Equal("ChangePassword", redirect.ActionName);
'        }
'        [Fact]
'        public async Task SetPassword_GET_WhenUserHasNoPassword_ReturnsViewWithModel()
'        {
'            // Arrange
'            var testUser = new ApplicationUser();
'            var statusMessage = "Test message";
'            _controller.StatusMessage = statusMessage;

'            _mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
'                .ReturnsAsync(testUser);
'            _mockUserManager.Setup(x => x.HasPasswordAsync(testUser))
'                .ReturnsAsync(false);

'            // Act
'            var result = await _controller.SetPassword();

'            // Assert
'            var viewResult = Assert.IsType<ViewResult>(result);
'            var model = Assert.IsType<SetPasswordViewModel>(viewResult.Model);
'            Assert.Equal(statusMessage, model.StatusMessage);
'        }
'        [Fact]
'        public async Task SetPassword_GET_WhenUserNotFound_ThrowsException()
'        {
'            // Arrange
'            _mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
'                .ReturnsAsync((ApplicationUser)null);

'            // Act & Assert
'            await Assert.ThrowsAsync<ApplicationException>(() => _controller.SetPassword());
'        }
'        [Fact]
'        public async Task SetPassword_POST_ValidRequest_SetsPasswordAndSignsIn()
'        {
'            // Arrange
'            var testUser = new ApplicationUser();
'            var model = new SetPasswordViewModel { NewPassword = "newPassword123!" };

'            _mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
'                .ReturnsAsync(testUser);
'            _mockUserManager.Setup(x => x.AddPasswordAsync(testUser, model.NewPassword))
'                .ReturnsAsync(IdentityResult.Success);
'            _mockSignInManager.Setup(x => x.SignInAsync(testUser, false, null))
'                .Returns(Task.CompletedTask);

'            // Act
'            var result = await _controller.SetPassword(model);

'            // Assert
'            _mockUserManager.Verify(x => x.AddPasswordAsync(testUser, model.NewPassword), Times.Once);
'            _mockSignInManager.Verify(x => x.SignInAsync(testUser, false, null), Times.Once);
'            Assert.Equal("Your password has been set.", _controller.StatusMessage);
'            var redirect = Assert.IsType<RedirectToActionResult>(result);
'            Assert.Equal("SetPassword", redirect.ActionName);
'        }
'        [Fact]
'        public async Task SetPassword_POST_InvalidModel_ReturnsViewWithModel()
'        {
'            // Arrange
'            var model = new SetPasswordViewModel();
'            _controller.ModelState.AddModelError("NewPassword", "Password is required");

'            // Act
'            var result = await _controller.SetPassword(model);

'            // Assert
'            var viewResult = Assert.IsType<ViewResult>(result);
'            Assert.Same(model, viewResult.Model);
'            _mockUserManager.Verify(x => x.AddPasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
'        }
'        [Fact]
'        public async Task SetPassword_POST_WhenAddPasswordFails_ReturnsViewWithErrors()
'        {
'            // Arrange
'            var testUser = new ApplicationUser();
'            var model = new SetPasswordViewModel { NewPassword = "weak" };
'            var errors = new[] { new IdentityError { Description = "Password too weak" } };

'            _mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
'                .ReturnsAsync(testUser);
'            _mockUserManager.Setup(x => x.AddPasswordAsync(testUser, model.NewPassword))
'                .ReturnsAsync(IdentityResult.Failed(errors));

'            // Act
'            var result = await _controller.SetPassword(model);

'            // Assert
'            var viewResult = Assert.IsType<ViewResult>(result);
'            Assert.False(_controller.ModelState.IsValid);
'            Assert.Contains("Password too weak", _controller.ModelState[string.Empty].Errors[0].ErrorMessage);
'        }
'        [Fact]
'        public async Task SetPassword_POST_WhenUserNotFound_ThrowsException()
'        {
'            // Arrange
'            var model = new SetPasswordViewModel();
'            _mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
'                .ReturnsAsync((ApplicationUser)null);

'            // Act & Assert
'            await Assert.ThrowsAsync<ApplicationException>(() => _controller.SetPassword(model));
'        }


'        #endregion
'    }
'}