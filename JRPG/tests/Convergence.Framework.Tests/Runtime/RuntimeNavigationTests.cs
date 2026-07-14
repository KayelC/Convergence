using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Logic.Runtime;
using Xunit;

namespace Convergence.Tests.Runtime;

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
        Assert.Same(initial, visited.Before);
        Assert.Equal(Id("crystal_garden"), visited.After.CurrentLocationId);
        RuntimeNavigationEvent appliedEvent = Assert.Single(visited.Events);
        Assert.Equal(RuntimeNavigationEventKind.TransitionApplied, appliedEvent.Kind);
        Assert.Equal(outbound.Id, appliedEvent.TransitionId);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<RuntimeNavigationEvent>)visited.Events).Add(appliedEvent));

        Assert.False(wrongDirection.Applied);
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

    private static ContentId Id(string value) => ContentId.Parse(value);

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
