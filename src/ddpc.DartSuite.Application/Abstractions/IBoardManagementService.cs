using ddpc.DartSuite.Application.Contracts.Boards;

namespace ddpc.DartSuite.Application.Abstractions;

public interface IBoardManagementService
{
    Task<IReadOnlyList<BoardDto>> GetBoardsAsync(CancellationToken cancellationToken = default);
    Task<BoardDto> CreateBoardAsync(CreateBoardRequest request, CancellationToken cancellationToken = default);
    Task<BoardDto?> UpdateBoardAsync(UpdateBoardRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteBoardAsync(Guid id, CancellationToken cancellationToken = default);
    Task<BoardDto?> UpdateBoardStatusAsync(Guid id, string status, string? externalMatchId = null, CancellationToken cancellationToken = default);
    Task<BoardDto?> SetManagedModeAsync(Guid id, string mode, Guid? tournamentId, CancellationToken cancellationToken = default);
    Task<BoardDto?> SetCurrentMatchAsync(Guid id, Guid? matchId, string? matchLabel, CancellationToken cancellationToken = default);
    Task<bool> HeartbeatAsync(Guid id, CancellationToken cancellationToken = default);
}