using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Xunit;

namespace Microsoft.eShopWeb.FunctionalTests.Web;

public static class WebPageHelpers
{
    public static string TokenTag = "__RequestVerificationToken";

    public static string GetRequestVerificationToken(string input)
    {
        string regexpression = @"name=""__RequestVerificationToken"" type=""hidden"" value=""([-A-Za-z0-9+=/\\_]+?)""";
        return RegexSearch(regexpression, input);
    }

    public static string GetId(string input)
    {
        string regexpression = @"name=""Items\[0\].Id"" value=""(\d)""";
        return RegexSearch(regexpression, input);
    }

    /// <summary>Signs in through the real login page and asserts the sign-in succeeded.</summary>
    public static async Task LoginAsync(HttpClient client, string email, string password = TestApplication.DefaultPassword)
    {
        var response = await PostLoginAsync(client, email, password);
        Assert.True(response.StatusCode == HttpStatusCode.Redirect,
            $"Login for {email} should redirect on success but returned {(int)response.StatusCode} {response.StatusCode}.");
        Assert.DoesNotContain("/Account/Login", response.Headers.Location?.OriginalString ?? string.Empty);
    }

    /// <summary>Posts the login form (requires a client created with AllowAutoRedirect = false).</summary>
    public static async Task<HttpResponseMessage> PostLoginAsync(HttpClient client, string email, string password)
    {
        var token = await GetFormTokenAsync(client, "/identity/account/login");
        return await PostFormAsync(client, "/identity/account/login", token,
            ("Email", email),
            ("Password", password));
    }

    /// <summary>GETs a page, asserts it rendered and returns its anti-forgery token.</summary>
    public static async Task<string> GetFormTokenAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"GET {url} should return 200 but returned {(int)response.StatusCode} {response.StatusCode}.");
        var token = GetRequestVerificationToken(await response.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrEmpty(token), $"No anti-forgery token found on {url}.");
        return token;
    }

    public static Task<HttpResponseMessage> PostFormAsync(HttpClient client, string url, string token,
        params (string Key, string Value)[] fields)
    {
        var values = fields.Select(f => new KeyValuePair<string, string>(f.Key, f.Value)).ToList();
        values.Add(new KeyValuePair<string, string>(TokenTag, token));
        return client.PostAsync(url, new FormUrlEncodedContent(values));
    }

    public static void AssertRedirectTo(HttpResponseMessage response, string expectedPath)
    {
        Assert.True(response.StatusCode == HttpStatusCode.Redirect,
            $"Expected a redirect to {expectedPath} but got {(int)response.StatusCode} {response.StatusCode}.");
        var location = response.Headers.Location;
        var path = location == null ? string.Empty
            : location.IsAbsoluteUri ? location.PathAndQuery : location.OriginalString;
        Assert.True(path.StartsWith(expectedPath, StringComparison.OrdinalIgnoreCase),
            $"Expected redirect to {expectedPath} but was '{location}'.");
    }

    public static void AssertRedirectToLogin(HttpResponseMessage response) =>
        AssertRedirectTo(response, "/Identity/Account/Login");

    /// <summary>Computes the current authenticator code for a base32 key, exactly as an authenticator app would.</summary>
    public static string ComputeTotp(string base32Key)
    {
        var key = FromBase32(base32Key);
        var timestep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        var counter = BitConverter.GetBytes(timestep);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(counter);
        }

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(counter);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24)
                     | (hash[offset + 1] << 16)
                     | (hash[offset + 2] << 8)
                     | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6");
    }

    private static byte[] FromBase32(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        input = input.Replace(" ", string.Empty).TrimEnd('=').ToUpperInvariant();
        var output = new List<byte>();
        int buffer = 0, bitsLeft = 0;
        foreach (var c in input)
        {
            buffer = (buffer << 5) | alphabet.IndexOf(c);
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                output.Add((byte)(buffer >> (bitsLeft - 8)));
                bitsLeft -= 8;
            }
        }
        return output.ToArray();
    }

    private static string RegexSearch(string regexpression, string input)
    {
        var regex = new Regex(regexpression);
        var match = regex.Match(input);
        return match!.Groups!.Values!.LastOrDefault()!.Value;
    }
}
