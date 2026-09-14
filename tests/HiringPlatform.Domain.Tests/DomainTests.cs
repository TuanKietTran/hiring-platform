using HiringPlatform.Domain.Applications;
using HiringPlatform.Domain.Common;
using HiringPlatform.Domain.Iam;
using HiringPlatform.Domain.Identity;
using HiringPlatform.Domain.Jobs;

using Xunit;

using ApplicationId = HiringPlatform.Domain.Common.ApplicationId;

namespace HiringPlatform.Domain.Tests;

public sealed class DomainTests
{
    [Fact]
    public void EmailIsCanonicalAndValidated()
    {
        Assert.Equal("person@example.com", Email.Create(" Person@Example.COM ").Value);
        Assert.Throws<DomainException>(() => Email.Create("not-an-email"));
    }

    [Fact]
    public void MoneyRejectsCurrencyMismatch()
    {
        var usd = Money.Of(100, Currency.USD);
        Assert.Throws<DomainException>(() => usd.Add(Money.Of(100, Currency.EUR)));
    }

    [Fact]
    public void JobStatusEnforcesTransitionTable()
    {
        Assert.Equal(JobStatus.Open, JobStatus.Draft.TransitionTo(JobStatus.Open));
        Assert.Throws<InvalidTransitionException>(() => JobStatus.Closed.TransitionTo(JobStatus.Open));
    }

    [Fact]
    public void ApplicationPipelineCannotSkipStages()
    {
        Assert.Equal(ApplicationStage.Screening, ApplicationStage.Applied.TransitionTo(ApplicationStage.Screening));
        Assert.Throws<InvalidTransitionException>(() => ApplicationStage.Applied.TransitionTo(ApplicationStage.Offered));
    }

    [Fact]
    public void ExplicitCandidateAdvanceDenyOverridesOwnerAllow()
    {
        var candidate = new UserId(Guid.CreateVersion7());
        var app = ApplicationId.New();
        var request = new AccessRequest(
            new SubjectAttributes(candidate, null, Role.Candidate, false),
            new ResourceAttributes(ResourceType.Application, app.Value, candidate, CompanyId.New()),
            AccessAction.ApplicationAdvance,
            DateTimeOffset.UtcNow);

        var decision = PolicyEvaluator.Default.Evaluate(request);

        Assert.False(decision.IsAllowed);
        Assert.Contains(nameof(CandidateCannotSelfAdvancePolicy), decision.Reason);
    }

    [Fact]
    public void CompanyStaffCanManageOwnCompanyButNotAnother()
    {
        var own = CompanyId.New();
        var subject = new SubjectAttributes(UserId.New(), own, Role.Recruiter, false);
        var ownJob = new ResourceAttributes(ResourceType.Job, Guid.CreateVersion7(), null, own);
        var otherJob = new ResourceAttributes(ResourceType.Job, Guid.CreateVersion7(), null, CompanyId.New());

        Assert.True(PolicyEvaluator.Default.Evaluate(new(subject, ownJob, AccessAction.JobWrite, DateTimeOffset.UtcNow)).IsAllowed);
        Assert.False(PolicyEvaluator.Default.Evaluate(new(subject, otherJob, AccessAction.JobWrite, DateTimeOffset.UtcNow)).IsAllowed);
    }
}
