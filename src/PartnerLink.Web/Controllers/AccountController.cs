using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PartnerLink.Core;
using PartnerLink.Core.Entities;
using PartnerLink.Core.Services;
using PartnerLink.Web.ViewModels;

namespace PartnerLink.Web.Controllers;

public class AccountController(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    IOrganizationService organizations,
    TimeProvider clock) : Controller
{
    [HttpGet, AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost, AllowAnonymous]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid) return View(model);

        var result = await signInManager.PasswordSignInAsync(
            model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? LocalRedirect(returnUrl)
                : RedirectToAction("Index", "Home");
        }

        ModelState.AddModelError(string.Empty, result.IsLockedOut
            ? "Too many failed attempts. Try again in a few minutes."
            : "Invalid e-mail or password.");
        return View(model);
    }

    [HttpGet, AllowAnonymous]
    public IActionResult Register() => View(new RegisterViewModel());

    [HttpPost, AllowAnonymous]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);

        if (await organizations.OidExistsAsync(model.OidCode, null, ct))
        {
            ModelState.AddModelError(nameof(model.OidCode), "An organization with this OID is already registered.");
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName.Trim(),
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime
        };

        var created = await userManager.CreateAsync(user, model.Password);
        if (!created.Succeeded)
        {
            foreach (var error in created.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        await userManager.AddToRoleAsync(user, Roles.Organization);
        await organizations.CreateAsync(user.Id, model.ToInput(), ct);
        await signInManager.SignInAsync(user, isPersistent: false);

        TempData["Success"] = "Welcome! Your organization is pending verification. You can browse projects now; publishing and applying unlock after an admin verifies your OID.";
        return RedirectToAction("Index", "Home");
    }

    [HttpPost, Authorize]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();
}
