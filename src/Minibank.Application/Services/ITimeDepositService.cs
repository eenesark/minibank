using MiniBank.DTOs;

namespace MiniBank.Services;

public interface ITimeDepositService
{
    /// <summary>
    /// Vade gün aralıklarına göre geçerli faiz oranı baremlerini döner
    /// (1-32 gün: %32, 33-90 gün: %38, 91+ gün: %41)
    /// </summary>
    IEnumerable<TimeDepositRateTierDto> GetRateTiers();

    /// <summary>
    /// Verilen anapara ve vade gün sayısına göre brüt/net faiz ve vade sonu getirisini hesaplar
    /// </summary>
    TimeDepositCalculateResultDto Calculate(TimeDepositCalculateDto dto);

    /// <summary>
    /// Müşterinin vadesiz hesabından parayı çekerek yeni vadeli mevduat hesabı açar
    /// </summary>
    Task<TimeDepositAccountDto> OpenTimeDepositAsync(Guid currentUserId, OpenTimeDepositDto dto);

    /// <summary>
    /// Vadeli hesabı kapatır (Vade dolduysa faiziyle birlikte, vadeden önce bozulduysa sadece anaparayı vadesiz hesaba aktarır)
    /// </summary>
    Task<CloseTimeDepositResultDto> CloseTimeDepositAsync(Guid currentUserId, Guid timeDepositAccountId, Guid? targetDemandAccountId = null);

    /// <summary>
    /// Müşterinin kendi vadeli hesaplarını (aktif ve geçmiş) listeler
    /// </summary>
    Task<IEnumerable<TimeDepositAccountDto>> GetMyTimeDepositsAsync(Guid currentUserId);
}

