using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieHub.Dtos;
using MovieHub.Services.Auth;

namespace MovieHub.Controllers;

[Authorize]
[Route("api/profiles")]
public class ProfilesController : AppControllerBase
{
    private readonly IProfileService _profiles;

    public ProfilesController(IProfileService profiles) => _profiles = profiles;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct) => Ok(await _profiles.GetAllAsync(ct));

    [AllowAnonymous]
    [HttpGet("avatars")]
    public IActionResult Avatars() => Ok(ProfileService.Avatars);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ProfileRequest request, CancellationToken ct)
        => Created(await _profiles.CreateAsync(request, ct), "Perfil criado.");

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] ProfileRequest request, CancellationToken ct)
        => Ok(await _profiles.UpdateAsync(id, request, ct), "Perfil atualizado.");

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _profiles.DeleteAsync(id, ct);
        return Done("Perfil removido.");
    }
}
