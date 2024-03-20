using System.Threading.Tasks;
using LinkDotNet.Blog.Web.Authentication;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LinkDotNet.Blog.Web.Pages;

public sealed partial class LoginModel(ILoginManager loginManager) : PageModel
{
    private readonly ILoginManager loginManager = loginManager;

    public async Task OnGet(string redirectUri) => await loginManager.SignInAsync(redirectUri);
}
