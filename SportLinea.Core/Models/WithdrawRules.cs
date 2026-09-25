namespace SportLinea.Models;

public static class WithdrawRules
{
    public const decimal MinAmount = 2000m;
    /// <summary>Суммы от этого порога включительно требуют подтверждения букмекера.</summary>
    public const decimal BookmakerApprovalThreshold = 1_000_000m;
}
