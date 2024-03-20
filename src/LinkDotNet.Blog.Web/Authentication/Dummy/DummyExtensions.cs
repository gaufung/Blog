using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace LinkDotNet.Blog.Web.Authentication.Dummy;

public static class DummyExtensions
{
    public static void UseDummyAuthentication(this IServiceCollection services)
    {
        _ = services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(
                CookieAuthenticationDefaults.AuthenticationScheme,
                options =>
                {
                    options.LoginPath = new PathString("/login");
                });

        _ = services.AddAuthorization();
        _ = services.AddHttpContextAccessor();
        _ = services.AddScoped<ILoginManager, DummyLoginManager>();
    }
}
