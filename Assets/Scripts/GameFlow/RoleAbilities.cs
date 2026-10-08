public readonly struct RoleAbilityContext
{
    public LiarPainterGamePhase Phase { get; }
    public int StrokeId { get; }
    public string StrokeOwnerPlayerId { get; }

    public RoleAbilityContext(LiarPainterGamePhase phase, int strokeId, string strokeOwnerPlayerId)
    {
        Phase = phase;
        StrokeId = strokeId;
        StrokeOwnerPlayerId = strokeOwnerPlayerId;
    }
}

public readonly struct RoleAbilityResult
{
    public bool Succeeded { get; }
    public string RevealedPlayerId { get; }

    public RoleAbilityResult(bool succeeded, string revealedPlayerId = null)
    {
        Succeeded = succeeded;
        RevealedPlayerId = revealedPlayerId;
    }
}

public interface IRoleAbility
{
    int RemainingUses { get; }
    RoleAbilityResult TryUse(RoleAbilityContext context);
}

public sealed class NoRoleAbility : IRoleAbility
{
    public int RemainingUses => 0;
    public RoleAbilityResult TryUse(RoleAbilityContext context) => new(false);
}

public sealed class AppraiserRoleAbility : IRoleAbility
{
    public int RemainingUses { get; private set; } = 1;

    public RoleAbilityResult TryUse(RoleAbilityContext context)
    {
        if (RemainingUses <= 0 || context.Phase != LiarPainterGamePhase.Discussion ||
            context.StrokeId <= 0 || string.IsNullOrWhiteSpace(context.StrokeOwnerPlayerId))
            return new RoleAbilityResult(false);

        RemainingUses--;
        return new RoleAbilityResult(true, context.StrokeOwnerPlayerId);
    }
}

public static class RoleAbilityFactory
{
    public static IRoleAbility Create(LiarPainterRole role)
    {
        return role == LiarPainterRole.Appraiser
            ? new AppraiserRoleAbility()
            : new NoRoleAbility();
    }
}
