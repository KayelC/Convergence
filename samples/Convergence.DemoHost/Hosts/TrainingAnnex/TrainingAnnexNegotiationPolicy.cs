using Convergence.Hosting;
using Convergence.Encounters;

namespace Convergence.DemoHost.TrainingAnnex;

internal sealed class TrainingAnnexNegotiationPolicy : INegotiationSessionPolicy
{
    public int QuestionLimit => 3;
    public int PositiveMoodThreshold => 4;
    public int NeutralMoodThreshold => 1;

    public NegotiationGateDecision EvaluateGate(NegotiationSessionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new NegotiationGateDecision(true);
    }

    public bool CanBegin(NegotiationSessionRequest request, IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(random);
        return true;
    }

    public NegotiationFamiliarGift SelectFamiliarGift(
        NegotiationSessionRequest request,
        IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(random);
        return NegotiationFamiliarGift.None;
    }

    public IReadOnlyList<NegotiationRuntimeDemand> CreateFallbackDemands(
        NegotiationSessionRequest request,
        IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(random);
        return Array.AsReadOnly(Array.Empty<NegotiationRuntimeDemand>());
    }

    public bool ResolveDemandlessSuccess(NegotiationSessionRequest request, IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(random);
        return true;
    }
}
