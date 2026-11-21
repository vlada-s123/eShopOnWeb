namespace Microsoft.AspNetCore.Mvc;

public static class UrlHelperExtensions
{
    public static string? EmailConfirmationLink(this IUrlHelper urlHelper, string userId, string code, string scheme)
    {
        return urlHelper.Page(
            pageName: "/Account/ConfirmEmail",
            pageHandler: null,
            values: new { area = "Identity", userId, code },
            protocol: scheme);
    }
}
