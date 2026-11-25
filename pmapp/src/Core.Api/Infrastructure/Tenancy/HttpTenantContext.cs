using System.Security.Claims;
using PMApp.Plugin.Abstractions;

namespace Core.Api.Infrastructure.Tenancy;

public sealed class HttpTenantContext(IHttpContextAccessor accessor) : ITenantContext
{
    private Guid? _overrideTenantId;
    private Guid? _overrideUserId;

    public void SetOverride(Guid tenantId, Guid? userId = null)
    {
        _overrideTenantId = tenantId;
        if (userId.HasValue) _overrideUserId = userId;
    }

    public Guid CurrentTenantId
    {
        get
        {
            if (_overrideTenantId.HasValue) return _overrideTenantId.Value;
            var claim = accessor.HttpContext?.User.FindFirstValue("tenant_id");
            return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }
    }

    public Guid? CurrentUserId
    {
        get
        {
            if (_overrideUserId.HasValue) return _overrideUserId.Value;
            var claim = accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? accessor.HttpContext?.User.FindFirstValue("sub");
            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }

    public bool IsSuperAdmin =>
        string.Equals(accessor.HttpContext?.User.FindFirstValue("is_super_admin"), "true", StringComparison.OrdinalIgnoreCase);

    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => accessor.HttpContext?.Request.Headers.UserAgent.ToString();
}
