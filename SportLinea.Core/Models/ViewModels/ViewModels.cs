using System.ComponentModel.DataAnnotations;
using SportLinea.Models;

namespace SportLinea.Models.ViewModels;

public class RegisterViewModel
{
    [Required(ErrorMessage = "Укажите email")]
    [EmailAddress(ErrorMessage = "Некорректный email")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Укажите пароль")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Пароль должен быть от 6 до 100 символов")]
    [DataType(DataType.Password)]
    [Display(Name = "Пароль")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Подтвердите пароль")]
    [Compare(nameof(Password), ErrorMessage = "Пароли не совпадают")]
    [DataType(DataType.Password)]
    [Display(Name = "Подтверждение пароля")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Укажите фамилию")]
    [Display(Name = "Фамилия")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Укажите имя")]
    [Display(Name = "Имя")]
    public string FirstName { get; set; } = string.Empty;

    [Display(Name = "Отчество")]
    public string? Patronymic { get; set; }

    [Required(ErrorMessage = "Укажите страну проживания")]
    [Display(Name = "Страна проживания")]
    public CountryOfResidence CountryOfResidence { get; set; } = CountryOfResidence.USA;
}

public class LoginViewModel
{
    [Required(ErrorMessage = "Укажите email")]
    [EmailAddress]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Укажите пароль")]
    [DataType(DataType.Password)]
    [Display(Name = "Пароль")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Запомнить меня")]
    public bool RememberMe { get; set; }
}

public class ProfileEditViewModel
{
    [Required]
    [Display(Name = "Фамилия")]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Имя")]
    public string FirstName { get; set; } = string.Empty;

    [Display(Name = "Отчество")]
    public string? Patronymic { get; set; }

    [Required(ErrorMessage = "Укажите страну проживания")]
    [Display(Name = "Страна проживания")]
    public CountryOfResidence CountryOfResidence { get; set; } = CountryOfResidence.USA;

    [Display(Name = "Новый пароль")]
    [DataType(DataType.Password)]
    public string? NewPassword { get; set; }

    [Display(Name = "Подтверждение пароля")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Пароли не совпадают")]
    public string? ConfirmPassword { get; set; }
}

public class DepositViewModel
{
    [Required(ErrorMessage = "Укажите сумму")]
    [Display(Name = "Сумма пополнения")]
    public decimal Amount { get; set; }
}

public class WithdrawViewModel
{
    [Required(ErrorMessage = "Укажите сумму")]
    [Display(Name = "Сумма вывода")]
    public decimal Amount { get; set; }
}

public class PlaceBetViewModel
{
    public int SportEventId { get; set; }
    public int CoefficientId { get; set; }

    [Required(ErrorMessage = "Укажите сумму ставки")]
    [Range(typeof(decimal), "100", "1000000", ErrorMessage = "Минимальная ставка — 100 ₽")]
    [Display(Name = "Сумма ставки")]
    public decimal Amount { get; set; }

    public bool UseFreeBet { get; set; }
}

public class CreateEventViewModel
{
    [Required(ErrorMessage = "Укажите категорию")]
    [Display(Name = "Категория")]
    public string SportCategory { get; set; } = SportCategories.Professional;

    [Required(ErrorMessage = "Укажите вид спорта")]
    [Display(Name = "Вид спорта")]
    public string SportType { get; set; } = SportTypes.GetForCategory(SportCategories.Professional).First();

    [Required(ErrorMessage = "Укажите название")]
    [Display(Name = "Название события")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Укажите дату и время")]
    [Display(Name = "Дата и время начала")]
    public DateTime StartDate { get; set; } = DateTime.Now.AddDays(1);

    [Required]
    [Display(Name = "Исход 1")]
    public string Outcome1 { get; set; } = "Победа 1";

    [Required]
    [Range(1.01, 100)]
    [Display(Name = "Коэффициент 1")]
    public decimal Coefficient1 { get; set; } = 2.0m;

    [Required]
    [Display(Name = "Исход 2")]
    public string Outcome2 { get; set; } = "Ничья";

    [Required]
    [Range(1.01, 100)]
    [Display(Name = "Коэффициент 2")]
    public decimal Coefficient2 { get; set; } = 3.0m;

    [Required]
    [Display(Name = "Исход 3")]
    public string Outcome3 { get; set; } = "Победа 2";

    [Required]
    [Range(1.01, 100)]
    [Display(Name = "Коэффициент 3")]
    public decimal Coefficient3 { get; set; } = 2.5m;
}

public class EditEventViewModel
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Вид спорта")]
    public string SportType { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Название события")]
    public string Title { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Дата и время начала")]
    public DateTime StartDate { get; set; }

    public bool HasBets { get; set; }
}

public class SetResultViewModel
{
    public int SportEventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;

    [Required(ErrorMessage = "Выберите выигрышный исход")]
    [Display(Name = "Выигрышный исход")]
    public int WinningCoefficientId { get; set; }

    [Required]
    [Display(Name = "Результат матча")]
    public string Result { get; set; } = string.Empty;
}

public class CreateBookmakerViewModel
{
    [Required]
    [EmailAddress]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 6)]
    [DataType(DataType.Password)]
    [Display(Name = "Пароль")]
    public string Password { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Фамилия")]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Имя")]
    public string FirstName { get; set; } = string.Empty;
}

public class AssignBonusViewModel
{
    public string PlayerId { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Выберите тип бонуса")]
    [Display(Name = "Тип бонуса")]
    public BookmakerBonusKind BonusKind { get; set; } = BookmakerBonusKind.FreeBet;

    [Range(1, 1_000_000, ErrorMessage = "Сумма фрибета — от 1 до 1 000 000 ₽")]
    [Display(Name = "Сумма фрибета, ₽")]
    public decimal FreeBetAmount { get; set; } = 2000;

    [Range(1, 1000, ErrorMessage = "Количество фриспинов — от 1 до 1000")]
    [Display(Name = "Количество фриспинов")]
    public int SpinCount { get; set; } = 10;

    [Range(1, 100_000, ErrorMessage = "Номинал фриспина — от 1 до 100 000 ₽")]
    [Display(Name = "Номинал фриспина, ₽")]
    public decimal SpinAmount { get; set; } = 200;

    [Display(Name = "Комментарий букмекера")]
    [StringLength(500, ErrorMessage = "Комментарий — не более 500 символов")]
    public string? BookmakerComment { get; set; }

    [Required]
    [Display(Name = "Действует до")]
    public DateTime EndDate { get; set; } = DateTime.Now.AddDays(30);
}

public enum BookmakerBonusKind
{
    FreeBet,
    FreeRouletteSpins
}

public class BlockPlayerViewModel
{
    public string PlayerId { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Укажите причину блокировки")]
    [StringLength(500, MinimumLength = 5, ErrorMessage = "Причина — от 5 до 500 символов")]
    [Display(Name = "Причина блокировки")]
    public string BlockReason { get; set; } = string.Empty;
}

public class BlockStaffViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Укажите причину блокировки")]
    [StringLength(500, MinimumLength = 5, ErrorMessage = "Причина — от 5 до 500 символов")]
    [Display(Name = "Причина блокировки")]
    public string BlockReason { get; set; } = string.Empty;
}

public class BlockedAccountInfoViewModel
{
    public string Reason { get; set; } = string.Empty;
    public string BookmakerName { get; set; } = string.Empty;
    public string BookmakerEmail { get; set; } = string.Empty;
}

public class PlayerRatingViewModel
{
    public string PlayerId { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public int Wins { get; set; }
    public int Losses { get; set; }
    public decimal TotalWinnings { get; set; }
}

public class PlayerStatsViewModel
{
    public int TotalBets { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Pending { get; set; }
    public decimal TotalWinnings { get; set; }
    public decimal TotalStaked { get; set; }
    public RouletteStatsViewModel Roulette { get; set; } = new();
    public PlinkoStatsViewModel Plinko { get; set; } = new();
    public ZeusStatsViewModel Zeus { get; set; } = new();
    public CamelotStatsViewModel Camelot { get; set; } = new();
    public FruitlandStatsViewModel Fruitland { get; set; } = new();
}

public class ZeusStatsViewModel
{
    public int TotalSpins { get; set; }
    public int SuccessfulSpins { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal WonZeus { get; set; }
    public decimal WonHades { get; set; }
    public decimal WonConfrontation { get; set; }

    public double WinPercent => TotalSpins > 0 ? SuccessfulSpins * 100.0 / TotalSpins : 0;
}

public class PlinkoStatsViewModel
{
    public int TotalSpins { get; set; }
    public int SuccessfulSpins { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal TotalWon { get; set; }
    public decimal WonBountyHunter { get; set; }
    public int MaxBountyMultiplier { get; set; }

    public double WinPercent => TotalSpins > 0 ? SuccessfulSpins * 100.0 / TotalSpins : 0;
}

public class CamelotStatsViewModel
{
    public int TotalSpins { get; set; }
    public int SuccessfulSpins { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal TotalWon { get; set; }
    public decimal WonBonus { get; set; }
    public decimal MaxCollectorNominal { get; set; }

    public double WinPercent => TotalSpins > 0 ? SuccessfulSpins * 100.0 / TotalSpins : 0;
}

public class FruitlandStatsViewModel
{
    public int TotalSpins { get; set; }
    public int SuccessfulSpins { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal TotalWon { get; set; }
    public decimal WonBonus { get; set; }
    public int MaxGlobalMultiplier { get; set; }

    public double WinPercent => TotalSpins > 0 ? SuccessfulSpins * 100.0 / TotalSpins : 0;
}

public class RouletteStatsViewModel
{
    public int TotalSpins { get; set; }
    public int SuccessfulSpins { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal TotalWon { get; set; }
    public decimal WonHotSpins { get; set; }
    public decimal WonCrazy { get; set; }

    public double WinPercent => TotalSpins > 0 ? SuccessfulSpins * 100.0 / TotalSpins : 0;
}

public class HomeViewModel
{
    public List<SportEvent> TopEvents { get; set; } = new();
    public List<SportEvent> NewEvents { get; set; } = new();
    public bool ShowRoulette { get; set; }
    public bool ShowPlinko { get; set; }
    public bool ShowZeusHades { get; set; }
    public bool ShowCamelotGold { get; set; }
    public bool ShowFruitland1000 { get; set; }
    public bool ShowEmptyLineRefresh { get; set; }
    public bool CrazyTimeAllowed { get; set; } = true;
    public decimal? PlayerBalance { get; set; }
    public int FreeRouletteSpinsRemaining { get; set; }
    public decimal FreeRouletteSpinAmount { get; set; }
    public bool FreePlinkoBountyAvailable { get; set; }
    public decimal FreePlinkoBountyStake { get; set; }
}
