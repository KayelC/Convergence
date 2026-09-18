using Convergence.Content;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Runtime;

public sealed class RuntimeNavigationTests
{
    [Fact]
    public void Navigation_UsesArbitraryLocationIdsAndRequiresAnExplicitReverseTransition()
    {
        var policy = new MutableNavigationPolicy { IsAllowed = true };
        var service = new RuntimeNavigationService(policy);
        var initial = new RuntimeNavigationSnapshot(Id("orbital_station"));
        var outbound = new RuntimeNavigationTransition(
            Id("visit_crystal_garden"),
            Id("orbital_station"),
            Id("crystal_garden"));
        var inbound = new RuntimeNavigationTransition(
            Id("return_to_station"),
            Id("crystal_garden"),
            Id("orbital_station"));

        RuntimeNavigationResult visited = service.Navigate(initial, outbound);
        RuntimeNavigationResult wrongDirection = service.Navigate(visited.After, outbound);

        Assert.True(visited.Applied);
        Assert.Null(visited.InvalidField);
        Assert.Same(initial, visited.Before);
        Assert.Equal(Id("crystal_garden"), visited.After.CurrentLocationId);
        RuntimeNavigationEvent appliedEvent = Assert.Single(visited.Events);
        Assert.Equal(RuntimeNavigationEventKind.TransitionApplied, appliedEvent.Kind);
        Assert.Equal(outbound.Id, appliedEvent.TransitionId);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<RuntimeNavigationEvent>)visited.Events).Add(appliedEvent));

        Assert.False(wrongDirection.Applied);
        Assert.Null(wrongDirection.InvalidField);
        Assert.Equal(RuntimeNavigationTransitionCode.SourceMismatch, wrongDirection.Code);
        Assert.Same(visited.After, wrongDirection.After);
        Assert.Equal(1, policy.EvaluationCount);

        RuntimeNavigationResult returned = service.Navigate(visited.After, inbound);
        Assert.True(returned.Applied);
        Assert.Equal(initial.CurrentLocationId, returned.After.CurrentLocationId);
        Assert.Equal(2, policy.EvaluationCount);
    }

    [Fact]
    public void Navigation_UsesInjectedPolicyAndPreservesStateWhenRejected()
    {
        var policy = new MutableNavigationPolicy
        {
            IsAllowed = false,
            ReasonId = Id("story_gate_locked"),
            Message = "The route unlocks later."
        };
        var service = new RuntimeNavigationService(policy);
        var initial = new RuntimeNavigationSnapshot(Id("chapter_hub"));
        var transition = new RuntimeNavigationTransition(
            Id("enter_memory"),
            Id("chapter_hub"),
            Id("memory_scene"));

        RuntimeNavigationResult rejected = service.Navigate(initial, transition);
        policy.IsAllowed = true;
        RuntimeNavigationResult accepted = service.Navigate(initial, transition);

        Assert.False(rejected.Applied);
        Assert.Null(rejected.InvalidField);
        Assert.Equal(RuntimeNavigationTransitionCode.PolicyRejected, rejected.Code);
        Assert.Same(initial, rejected.Before);
        Assert.Same(initial, rejected.After);
        Assert.Equal(Id("story_gate_locked"), rejected.ReasonId);
        Assert.Equal("The route unlocks later.", rejected.Message);
        Assert.Equal(RuntimeNavigationEventKind.TransitionRejected, Assert.Single(rejected.Events).Kind);

        Assert.True(accepted.Applied);
        Assert.Equal(Id("memory_scene"), accepted.After.CurrentLocationId);
        Assert.Equal(2, policy.EvaluationCount);
        Assert.Same(transition, policy.LastRequest!.Transition);
        Assert.Same(initial, policy.LastRequest.Current);
    }

    public static TheoryData<
        RuntimeNavigationSnapshot,
        RuntimeNavigationTransition,
        RuntimeNavigationRequestField> InvalidRequests =>
        new()
        {
            {
                new RuntimeNavigationSnapshot(default),
                ValidTransition(),
                RuntimeNavigationRequestField.CurrentLocationId
            },
            {
                ValidSnapshot(),
                new RuntimeNavigationTransition(default, Id("origin"), Id("destination")),
                RuntimeNavigationRequestField.TransitionId
            },
            {
                ValidSnapshot(),
                new RuntimeNavigationTransition(Id("travel"), default, Id("destination")),
                RuntimeNavigationRequestField.SourceLocationId
            },
            {
                ValidSnapshot(),
                new RuntimeNavigationTransition(Id("travel"), Id("origin"), default),
                RuntimeNavigationRequestField.DestinationLocationId
            }
        };

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public void Navigation_RejectsEmptyRequestIdsBeforeCallingThePolicy(
        RuntimeNavigationSnapshot current,
        RuntimeNavigationTransition transition,
        RuntimeNavigationRequestField expectedField)
    {
        var policy = new MutableNavigationPolicy { IsAllowed = true };
        var service = new RuntimeNavigationService(policy);

        RuntimeNavigationResult result = service.Navigate(current, transition);

        Assert.Equal(RuntimeNavigationTransitionCode.InvalidRequest, result.Code);
        Assert.False(result.Applied);
        Assert.Same(current, result.Before);
        Assert.Same(current, result.After);
        Assert.Same(transition, result.Transition);
        Assert.Equal(expectedField, result.InvalidField);
        Assert.Equal(Id("invalid_navigation_request"), result.ReasonId);
        Assert.Empty(result.Events);
        Assert.Equal(0, policy.EvaluationCount);
        Assert.Null(policy.LastRequest);
    }

    [Fact]
    public void Navigation_ReportsTheFirstInvalidFieldInStableValidationOrder()
    {
        var policy = new MutableNavigationPolicy { IsAllowed = true };
        var service = new RuntimeNavigationService(policy);
        var current = new RuntimeNavigationSnapshot(default);
        var transition = new RuntimeNavigationTransition(default, default, default);

        RuntimeNavigationResult result = service.Navigate(current, transition);

        Assert.Equal(RuntimeNavigationRequestField.CurrentLocationId, result.InvalidField);
        Assert.Equal(0, policy.EvaluationCount);
    }

    private static ContentId Id(string value) => ContentId.Parse(value);

    private static RuntimeNavigationSnapshot ValidSnapshot() => new(Id("origin"));

    private static RuntimeNavigationTransition ValidTransition() =>
        new(Id("travel"), Id("origin"), Id("destination"));

    private sealed class MutableNavigationPolicy : IRuntimeNavigationPolicy
    {
        public bool IsAllowed { get; set; }
        public ContentId? ReasonId { get; init; }
        public string? Message { get; init; }
        public int EvaluationCount { get; private set; }
        public RuntimeNavigationPolicyRequest? LastRequest { get; private set; }

        public RuntimeNavigationPolicyDecision Evaluate(RuntimeNavigationPolicyRequest request)
        {
            EvaluationCount++;
            LastRequest = request;
            return new RuntimeNavigationPolicyDecision(IsAllowed, ReasonId, Message);
        }
    }
}
