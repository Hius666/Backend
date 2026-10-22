using System.Security.Claims;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace DoanTrungHieu_2380600643.Api.Services;

public static class GoogleOAuth
{
    public const string Scheme="Google";
    public static bool Configured(IConfiguration config) => !string.IsNullOrWhiteSpace(config["Google:WebClientId"])
        && !string.IsNullOrWhiteSpace(config["Google:ClientSecret"]);
    public static void Register(WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<GoogleFlowStore>();
        if (!Configured(builder.Configuration)) return;
        builder.Services.AddAuthentication().AddCookie("GoogleExternal").AddOAuth(Scheme,o =>
        {
            o.SignInScheme="GoogleExternal";
            o.ClientId=builder.Configuration["Google:WebClientId"]!;
            o.ClientSecret=builder.Configuration["Google:ClientSecret"]!;
            o.AuthorizationEndpoint="https://accounts.google.com/o/oauth2/v2/auth";
            o.TokenEndpoint="https://oauth2.googleapis.com/token";
            o.UserInformationEndpoint="https://openidconnect.googleapis.com/v1/userinfo";
            o.CallbackPath="/signin-google";
            o.UsePkce=true;
            o.SaveTokens=false;
            o.Scope.Add("openid"); o.Scope.Add("email"); o.Scope.Add("profile");
            o.CorrelationCookie.SameSite=SameSiteMode.Lax;
            o.CorrelationCookie.SecurePolicy=builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            o.Events.OnRedirectToAuthorizationEndpoint=e =>
            {
                e.Response.Redirect(Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(e.RedirectUri,"prompt","select_account"));
                return Task.CompletedTask;
            };
            o.Events.OnCreatingTicket=async e =>
            {
                using var request=new HttpRequestMessage(HttpMethod.Get,e.Options.UserInformationEndpoint);
                request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",e.AccessToken);
                using var response=await e.Backchannel.SendAsync(request,e.HttpContext.RequestAborted);
                response.EnsureSuccessStatusCode();
                using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(e.HttpContext.RequestAborted));
                var profile=GoogleProfile.Parse(json.RootElement);
                e.Identity!.AddClaim(new Claim(ClaimTypes.NameIdentifier,profile.Subject));
                e.Identity.AddClaim(new Claim(ClaimTypes.Email,profile.Email));
                e.Identity.AddClaim(new Claim(ClaimTypes.Name,profile.Name));
            };
            o.Events.OnTicketReceived=e =>
            {
                e.HandleResponse();
                var id=Guid.Parse(e.Properties!.Items["GoogleFlowId"]!);
                var principal=e.Principal!;
                var profile=new GoogleProfile(principal.FindFirstValue(ClaimTypes.NameIdentifier)!,
                    principal.FindFirstValue(ClaimTypes.Email)!,principal.FindFirstValue(ClaimTypes.Name)!);
                var grant=e.HttpContext.RequestServices.GetRequiredService<GoogleFlowStore>().Complete(id,profile);
                e.Response.Headers.CacheControl="no-store";
                e.Response.Redirect(GoogleFlowStore.CallbackUri+"?code="+Uri.EscapeDataString(grant.Code)+"&state="+Uri.EscapeDataString(grant.State));
                return Task.CompletedTask;
            };
            o.Events.OnRemoteFailure=e =>
            {
                e.HandleResponse(); e.Response.Headers.CacheControl="no-store";
                e.Response.Redirect(GoogleFlowStore.CallbackUri+"?error=google_authorization_failed");
                return Task.CompletedTask;
            };
        });
    }
}
