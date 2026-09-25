namespace SportLinea.Models;

public enum UserStatus
{
    Active,
    Blocked
}

public enum EventStatus
{
    AcceptingBets,
    Completed,
    Cancelled
}

public enum BetStatus
{
    Accepted,
    Won,
    Lost
}

public enum OperationType
{
    Deposit,
    Withdrawal,
    BetWithdrawal,
    Win,
    RouletteStake,
    RouletteWin,
    RouletteBust,
    PlinkoStake,
    PlinkoWin,
    BalanceReset,
    RouletteSpin,
    PlinkoSpin,
    PlinkoBountyGame,
    ZeusHadesStake,
    ZeusHadesWin,
    ZeusHadesSpin,
    CamelotStake,
    CamelotWin,
    CamelotSpin,
    FruitlandStake,
    FruitlandWin,
    FruitlandSpin
}

public enum RouletteSpinKind
{
    Main = 1,
    HotSpins = 2,
    CrazyBonus = 3
}

public enum PlinkoSpinKind
{
    Main = 1,
    BountyHunter = 2
}

public enum ZeusSpinKind
{
    Main = 1,
    Confrontation = 2
}

public enum CamelotSpinKind
{
    Main = 1,
    Bonus = 2,
    BonusBuy = 3,
    FeatureBuy = 4
}

public enum FruitlandSpinKind
{
    Main = 1,
    Bonus = 2,
    BonusBuy = 3
}

public enum NotificationType
{
    Registration,
    BetResult,
    BetWin,
    BetLoss,
    Bonus,
    Info,
    WithdrawalPending,
    WithdrawalApproved,
    WithdrawalRejected,
    BalanceReset,
    BalanceDeposit,
    WithdrawalAccepted
}

public enum NotificationStatus
{
    Sent,
    Delivered,
    Read
}

public enum BonusStatus
{
    Pending,
    Active,
    Used,
    Expired
}

public enum BonusType
{
    Manual,
    FreeBet,
    FreeRouletteSpins,
    FreePlinkoBounty
}

public enum WithdrawalRequestStatus
{
    Pending,
    Approved,
    Rejected
}

public static class Roles
{
    public const string Player = "Игрок";
    public const string Bookmaker = "Букмекер";
    public const string SuperUser = "Суперпользователь";
}
