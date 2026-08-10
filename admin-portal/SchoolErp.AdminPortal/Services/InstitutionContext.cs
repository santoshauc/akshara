using Microsoft.JSInterop;
using SchoolErp.Domain.TenantCatalog;

namespace SchoolErp.AdminPortal.Services;

/// <summary>
/// What kind of institution the signed-in user belongs to, and the words that
/// follow from it. A college teaches semesters, not classes — and the same
/// database row is called both, so the difference belongs in one place rather
/// than in a conditional on every page that names it.
///
/// Loads itself once from the branding endpoint (which the layout fetches
/// anyway) so a page never has to depend on the layout having finished first.
/// </summary>
public sealed class InstitutionContext
{
    private readonly TenantsClient _tenants;
    private readonly IJSRuntime _js;
    private Task<InstitutionType>? _inFlight;

    public InstitutionContext(TenantsClient tenants, IJSRuntime js)
    {
        _tenants = tenants;
        _js = js;
    }

    public InstitutionType InstitutionType { get; private set; } = InstitutionType.School;

    public bool IsCollege => InstitutionType == InstitutionType.College;

    // --- the words -----------------------------------------------------------

    /// <summary>"Class" at a school, "Semester" at a college.</summary>
    public string Cohort => IsCollege ? "Semester" : "Class";

    public string CohortPlural => IsCollege ? "Semesters" : "Classes";

    /// <summary>Heading for the structure screen.</summary>
    public string CohortAndSection => IsCollege ? "Semesters & sections" : "Classes & sections";

    /// <summary>Placeholder that shows the shape of a name, not just its label.</summary>
    public string CohortNameHint => IsCollege
        ? "Cohort name (e.g. B.Com Semester 1)"
        : "Class name (e.g. Grade 5)";

    // --- loading -------------------------------------------------------------

    /// <summary>
    /// Resolves the institution type once per session. Concurrent callers
    /// share one request; failures fall back to School, which is the shape
    /// every screen already assumed.
    /// </summary>
    public async Task EnsureLoadedAsync()
    {
        _inFlight ??= LoadAsync();
        InstitutionType = await _inFlight;
    }

    /// <summary>
    /// Lets the layout hand over the branding it already fetched, so the
    /// common path costs no extra request.
    /// </summary>
    public void Adopt(InstitutionType institutionType)
    {
        InstitutionType = institutionType;
        _inFlight = Task.FromResult(institutionType);
    }

    private async Task<InstitutionType> LoadAsync()
    {
        try
        {
            var code = await _js.InvokeAsync<string?>(
                "localStorage.getItem", "akshara.schoolCode");
            if (string.IsNullOrWhiteSpace(code))
            {
                return InstitutionType.School; // platform user, or not signed in yet
            }

            var branding = await _tenants.GetBrandingAsync(code);
            return branding?.InstitutionType ?? InstitutionType.School;
        }
        catch (Exception)
        {
            // Wording is cosmetic; it must never take a page down with it.
            return InstitutionType.School;
        }
    }
}
