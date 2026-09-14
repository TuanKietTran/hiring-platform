using HiringPlatform.Application.Common;
using HiringPlatform.Domain.Common;
using HiringPlatform.Domain.Companies;
using HiringPlatform.Domain.Iam;
using HiringPlatform.Domain.Identity;

namespace HiringPlatform.Application.Identity;

public sealed record UserDto(
    Guid Id,
    string Email,
    string FullName,
    Role Role,
    Guid? CompanyId,
    string? CompanyName
)
{
    public static UserDto From(User u, Company? c = null) =>
        new(u.Id.Value, u.Email.Value, u.FullName, u.Role, u.CompanyId?.Value, c?.Name);
}

// ── Register candidate ──────────────────────────────────────────────────────

public sealed record RegisterCandidate(
    string Email,
    string Password,
    string FullName
) : ICommand<UserDto>;

public sealed class RegisterCandidateHandler(
    IUserRepository users,
    IPasswordHasher hasher,
    IUnitOfWork uow,
    IClock clock
)
    : IHandler<RegisterCandidate, UserDto>
{
    public async Task<Result<UserDto>> Handle(RegisterCandidate r, CancellationToken ct)
    {
        var email = Email.Create(r.Email);
        var plain = PlainPassword.Create(r.Password, PasswordValidator.Default);
        if (await users.FindByEmail(email, ct) is not null)
            return ApplicationError.Conflict("Email already registered");

        var user = User.RegisterCandidate(UserId.New(), email, hasher.Hash(plain), r.FullName, clock.UtcNow);
        users.Add(user);
        await uow.SaveChanges(ct);
        return UserDto.From(user);
    }
}

// ── Register company (creates the company and its first org admin) ──────────

public sealed record RegisterCompany(
    string CompanyName,
    string? Website,
    string Email,
    string Password,
    string FullName
)
    : ICommand<UserDto>;

public sealed class RegisterCompanyHandler(
    IUserRepository users,
    ICompanyRepository companies,
    IPasswordHasher hasher,
    IUnitOfWork uow,
    IClock clock
)
    : IHandler<RegisterCompany, UserDto>
{
    public async Task<Result<UserDto>> Handle(RegisterCompany r, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var email = Email.Create(r.Email);
        var plain = PlainPassword.Create(r.Password, PasswordValidator.Default);
        var company = Company.Create(CompanyId.New(), r.CompanyName, r.Website, now);

        if (await users.FindByEmail(email, ct) is not null)
            return ApplicationError.Conflict("Email already registered");
        if (await companies.SlugExists(company.Slug, ct))
            return ApplicationError.Conflict("A company with this name already exists");

        var admin = User.RegisterStaff(UserId.New(), email, hasher.Hash(plain), r.FullName, company.Id, Role.OrgAdmin, now);
        companies.Add(company);
        users.Add(admin);
        await uow.SaveChanges(ct);
        return UserDto.From(admin, company);
    }
}

// ── Add staff member (org admin only) ───────────────────────────────────────

public sealed record AddStaffMember(
    UserId ActorId,
    string Email,
    string Password,
    string FullName,
    Role Role
)
    : ICommand<UserDto>;

public sealed class AddStaffMemberHandler(
    AccessGuard guard,
    IUserRepository users,
    ICompanyRepository companies,
    IPasswordHasher hasher,
    IUnitOfWork uow,
    IClock clock
)
    : IHandler<AddStaffMember, UserDto>
{
    public async Task<Result<UserDto>> Handle(AddStaffMember r, CancellationToken ct)
    {
        var actor = await guard.Actor(r.ActorId, ct);
        if (!actor.IsSuccess)
            return actor.Error!;
        if (actor.Value!.CompanyId is not { } companyId)
            return ApplicationError.Forbidden("Only company staff can add staff");
        if (guard.Check(actor.Value, AccessGuard.OwnCompany(companyId), AccessAction.CompanyWrite) is { } denied)
            return denied;

        var email = Email.Create(r.Email);
        var plain = PlainPassword.Create(r.Password, PasswordValidator.Default);
        if (await users.FindByEmail(email, ct) is not null)
            return ApplicationError.Conflict("Email already registered");

        var user = User.RegisterStaff(UserId.New(), email, hasher.Hash(plain), r.FullName, companyId, r.Role, clock.UtcNow);
        users.Add(user);
        await uow.SaveChanges(ct);
        return UserDto.From(user, await companies.Find(companyId, ct));
    }
}

// ── Login ───────────────────────────────────────────────────────────────────

public sealed record Login(
    string Email,
    string Password
) : IQuery<UserDto>;

public sealed class LoginHandler(
    IUserRepository users,
    ICompanyRepository companies,
    IPasswordHasher hasher
)
    : IHandler<Login, UserDto>
{
    private static readonly ApplicationError Invalid = ApplicationError.Unauthorized("Invalid email or password");

    public async Task<Result<UserDto>> Handle(Login r, CancellationToken ct)
    {
        Email email;
        try
        {
            email = Email.Create(r.Email);
        }
        catch (DomainException) { return Invalid; }

        // same response for unknown email and wrong password
        var user = await users.FindByEmail(email, ct);
        if (user is null || !hasher.Verify(PlainPassword.Unchecked(r.Password), user.PasswordHash))
            return Invalid;

        var company = user.CompanyId is { } id ? await companies.Find(id, ct) : null;
        return UserDto.From(user, company);
    }
}

// ── Current user / staff directory ──────────────────────────────────────────

public sealed record GetUser(
    UserId Id
) : IQuery<UserDto>;

public sealed class GetUserHandler(
    IUserRepository users,
    ICompanyRepository companies
) : IHandler<GetUser, UserDto>
{
    public async Task<Result<UserDto>> Handle(GetUser r, CancellationToken ct)
    {
        var user = await users.Find(r.Id, ct);
        if (user is null)
            return ApplicationError.NotFound("User not found");
        var company = user.CompanyId is { } id ? await companies.Find(id, ct) : null;
        return UserDto.From(user, company);
    }
}

public sealed record ListStaff(
    UserId ActorId
) : IQuery<IReadOnlyList<UserDto>>;

public sealed class ListStaffHandler(
    AccessGuard guard,
    IUserRepository users
) : IHandler<ListStaff, IReadOnlyList<UserDto>>
{
    public async Task<Result<IReadOnlyList<UserDto>>> Handle(ListStaff r, CancellationToken ct)
    {
        var actor = await guard.Actor(r.ActorId, ct);
        if (!actor.IsSuccess)
            return actor.Error!;
        if (actor.Value!.CompanyId is not { } companyId)
            return ApplicationError.Forbidden("Only company staff can list staff");

        var staff = await users.ListStaff(companyId, ct);
        return staff.Select(u => UserDto.From(u)).ToList();
    }
}
