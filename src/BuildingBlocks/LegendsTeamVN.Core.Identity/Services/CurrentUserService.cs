using System.Security.Claims;

using LegendsTeamVN.Core.Identity.Abstractions;

using Microsoft.AspNetCore.Http;

namespace LegendsTeamVN.Core.Identity.Services;

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public string? UserName => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Name);
    
    public string? Email => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Email);
    
    public Guid? UserId 
    {
        get 
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var userIdString = user?.FindFirstValue(ClaimTypes.NameIdentifier) ?? 
                               user?.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub) ??
                               user?.FindFirstValue("sub") ??
                               user?.FindFirstValue("id") ??
                               user?.FindFirstValue("uid") ??
                               user?.FindFirstValue("userId");
            
            return Guid.TryParse(userIdString, out var userId) ? userId : null;
        }
    }
    
    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    public List<string>? Roles => _httpContextAccessor.HttpContext?.User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
    public List<string>? Permissions => _httpContextAccessor.HttpContext?.User?.FindAll("permissions").Select(c => c.Value).ToList();
}
