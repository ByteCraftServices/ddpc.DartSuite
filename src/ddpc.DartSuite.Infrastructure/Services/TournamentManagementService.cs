using ddpc.DartSuite.Application.Abstractions;
using ddpc.DartSuite.Application.Contracts.Tournaments;
using ddpc.DartSuite.Domain.Entities;
using ddpc.DartSuite.Domain.Enums;
using ddpc.DartSuite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ddpc.DartSuite.Infrastructure.Services;

public sealed class TournamentManagementService(DartSuiteDbContext dbContext) : ITournamentManagementService
{
    private const string CodeChars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    // ─── helper: build TournamentDto from entity ───
    private static TournamentDto ToDto(Tournament x, int participantCount) => new(
        x.Id, x.Name, x.OrganizerAccount, x.Status.ToString(), x.StartDate, x.EndDate,
        x.StartTime?.ToString("HH:mm"),
        x.Mode.ToString(), x.Variant.ToString(),
        x.TeamplayEnabled, x.IsLocked, x.AreGameModesLocked, x.JoinCode, participantCount,
        x.GroupCount, x.PlayoffAdvancers, x.KnockoutsPerRound, x.MatchesPerOpponent,
        x.GroupMode.ToString(), x.GroupDrawMode.ToString(), x.PlanningVariant.ToString(),
        x.GroupOrderMode.ToString(), x.ThirdPlaceMatch, x.PlayersPerTeam, x.WinPoints, x.LegFactor);

    // ─── Tournaments ───

    public async Task<IReadOnlyList<TournamentDto>> GetTournamentsAsync(CancellationToken cancellationToken = default)
    {
        var tournaments = await dbContext.Tournaments.AsNoTracking().ToListAsync(cancellationToken);
        var counts = await dbContext.Participants.AsNoTracking()
            .GroupBy(p => p.TournamentId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);

        return tournaments.Select(t => ToDto(t, counts.GetValueOrDefault(t.Id))).ToList();
    }

    public async Task<IReadOnlyList<TournamentDto>> GetTournamentsByHostAsync(string host, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var tournaments = await dbContext.Tournaments.AsNoTracking()
            .Where(x => x.OrganizerAccount == host && x.EndDate >= today)
            .ToListAsync(cancellationToken);
        var ids = tournaments.Select(t => t.Id).ToList();
        var counts = await dbContext.Participants.AsNoTracking()
            .Where(p => ids.Contains(p.TournamentId))
            .GroupBy(p => p.TournamentId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);

        return tournaments.Select(t => ToDto(t, counts.GetValueOrDefault(t.Id))).ToList();
    }

    public async Task<TournamentDto> CreateTournamentAsync(CreateTournamentRequest request, CancellationToken cancellationToken = default)
    {
        var mode = Enum.TryParse<TournamentMode>(request.Mode, true, out var parsedMode) ? parsedMode : TournamentMode.Knockout;
        var variant = Enum.TryParse<TournamentVariant>(request.Variant, true, out var parsedVariant) ? parsedVariant : TournamentVariant.Online;
        var joinCode = await GenerateUniqueJoinCodeAsync(cancellationToken);

        TimeOnly? startTime = null;
        if (!string.IsNullOrEmpty(request.StartTime) && TimeOnly.TryParse(request.StartTime, out var st))
            startTime = st;

        var tournament = new Tournament
        {
            Name = request.Name,
            OrganizerAccount = request.OrganizerAccount,
            StartDate = request.StartDate,
            EndDate = request.EndDate ?? request.StartDate,
            StartTime = startTime,
            TeamplayEnabled = request.TeamplayEnabled,
            Mode = mode,
            Variant = variant,
            JoinCode = joinCode
        };

        dbContext.Tournaments.Add(tournament);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(tournament, 0);
    }

    public async Task<TournamentDto?> GetTournamentAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var tournament = await dbContext.Tournaments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == tournamentId, cancellationToken);
        if (tournament is null) return null;
        var count = await dbContext.Participants.CountAsync(x => x.TournamentId == tournamentId, cancellationToken);
        return ToDto(tournament, count);
    }

    public async Task<TournamentDto?> GetTournamentByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var normalizedCode = code.ToUpperInvariant();
        var tournament = await dbContext.Tournaments.AsNoTracking().FirstOrDefaultAsync(x => x.JoinCode == normalizedCode, cancellationToken);
        if (tournament is null) return null;
        var count = await dbContext.Participants.CountAsync(x => x.TournamentId == tournament.Id, cancellationToken);
        return ToDto(tournament, count);
    }

    public async Task<TournamentDto?> UpdateTournamentAsync(UpdateTournamentRequest request, CancellationToken cancellationToken = default)
    {
        var tournament = await dbContext.Tournaments.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (tournament is null) return null;

        if (tournament.IsLocked)
            throw new InvalidOperationException("Das Turnier ist gesperrt. Bitte entsperren Sie es, bevor Sie Änderungen vornehmen.");

        var mode = Enum.TryParse<TournamentMode>(request.Mode, true, out var parsedMode) ? parsedMode : TournamentMode.Knockout;
        var variant = Enum.TryParse<TournamentVariant>(request.Variant, true, out var parsedVariant) ? parsedVariant : TournamentVariant.Online;

        tournament.Name = request.Name;
        tournament.OrganizerAccount = request.OrganizerAccount;
        tournament.StartDate = request.StartDate;
        tournament.EndDate = request.EndDate ?? request.StartDate;
        tournament.TeamplayEnabled = request.TeamplayEnabled;
        tournament.Mode = mode;
        tournament.Variant = variant;

        if (!string.IsNullOrEmpty(request.StartTime) && TimeOnly.TryParse(request.StartTime, out var st))
            tournament.StartTime = st;
        else if (request.StartTime is not null)
            tournament.StartTime = null;

        // Group settings
        tournament.GroupCount = request.GroupCount;
        tournament.PlayoffAdvancers = request.PlayoffAdvancers;
        tournament.KnockoutsPerRound = request.KnockoutsPerRound;
        tournament.MatchesPerOpponent = request.MatchesPerOpponent;
        if (Enum.TryParse<GroupMode>(request.GroupMode, true, out var gm)) tournament.GroupMode = gm;
        if (Enum.TryParse<GroupDrawMode>(request.GroupDrawMode, true, out var gd)) tournament.GroupDrawMode = gd;
        if (Enum.TryParse<PlanningVariant>(request.PlanningVariant, true, out var pv)) tournament.PlanningVariant = pv;
        if (Enum.TryParse<GroupOrderMode>(request.GroupOrderMode, true, out var go)) tournament.GroupOrderMode = go;

        // KO settings
        tournament.ThirdPlaceMatch = request.ThirdPlaceMatch;
        tournament.PlayersPerTeam = request.PlayersPerTeam;
        tournament.WinPoints = request.WinPoints;
        tournament.LegFactor = request.LegFactor;
        tournament.AreGameModesLocked = request.AreGameModesLocked;

        await dbContext.SaveChangesAsync(cancellationToken);
        var count = await dbContext.Participants.CountAsync(x => x.TournamentId == tournament.Id, cancellationToken);
        return ToDto(tournament, count);
    }

    public async Task<TournamentDto?> SetLockedAsync(Guid tournamentId, bool locked, CancellationToken cancellationToken = default)
    {
        var tournament = await dbContext.Tournaments.FirstOrDefaultAsync(x => x.Id == tournamentId, cancellationToken);
        if (tournament is null) return null;
        tournament.IsLocked = locked;
        if (locked) tournament.AreGameModesLocked = true;
        await dbContext.SaveChangesAsync(cancellationToken);
        var count = await dbContext.Participants.CountAsync(x => x.TournamentId == tournamentId, cancellationToken);
        return ToDto(tournament, count);
    }

    // ─── Participants ───

    public async Task<IReadOnlyList<ParticipantDto>> GetParticipantsAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Participants.AsNoTracking()
            .Where(x => x.TournamentId == tournamentId)
            .OrderBy(x => x.Seed)
            .Select(x => new ParticipantDto(x.Id, x.DisplayName, x.AccountName, x.IsAutodartsAccount, x.IsManager, x.Seed, x.GroupNumber, x.TeamId))
            .ToListAsync(cancellationToken);
    }

    public async Task<ParticipantDto> AddParticipantAsync(AddParticipantRequest request, CancellationToken cancellationToken = default)
    {
        var exists = await dbContext.Participants.AnyAsync(
            x => x.TournamentId == request.TournamentId && x.AccountName == request.AccountName,
            cancellationToken);
        if (exists) throw new InvalidOperationException("Participant already exists.");

        var participant = new Participant
        {
            TournamentId = request.TournamentId,
            DisplayName = request.DisplayName,
            AccountName = request.AccountName,
            IsAutodartsAccount = request.IsAutodartsAccount,
            IsManager = request.IsManager,
            Seed = request.Seed
        };

        dbContext.Participants.Add(participant);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new ParticipantDto(participant.Id, participant.DisplayName, participant.AccountName, participant.IsAutodartsAccount, participant.IsManager, participant.Seed, participant.GroupNumber, participant.TeamId);
    }

    public async Task<ParticipantDto?> UpdateParticipantAsync(UpdateParticipantRequest request, CancellationToken cancellationToken = default)
    {
        var participant = await dbContext.Participants
            .FirstOrDefaultAsync(x => x.Id == request.ParticipantId && x.TournamentId == request.TournamentId, cancellationToken);
        if (participant is null) return null;

        // Enforce at least 1 manager if removing manager flag
        if (participant.IsManager && !request.IsManager)
        {
            var managerCount = await dbContext.Participants.CountAsync(
                x => x.TournamentId == request.TournamentId && x.IsManager, cancellationToken);
            if (managerCount <= 1)
                throw new InvalidOperationException("Es muss mindestens ein Spielleiter vorhanden sein.");
        }

        participant.DisplayName = request.DisplayName;
        participant.AccountName = request.AccountName;
        participant.IsAutodartsAccount = request.IsAutodartsAccount;
        participant.IsManager = request.IsManager;
        participant.Seed = request.Seed;

        await dbContext.SaveChangesAsync(cancellationToken);
        return new ParticipantDto(participant.Id, participant.DisplayName, participant.AccountName, participant.IsAutodartsAccount, participant.IsManager, participant.Seed, participant.GroupNumber, participant.TeamId);
    }

    public async Task<bool> RemoveParticipantAsync(Guid tournamentId, Guid participantId, CancellationToken cancellationToken = default)
    {
        var participant = await dbContext.Participants
            .FirstOrDefaultAsync(x => x.Id == participantId && x.TournamentId == tournamentId, cancellationToken);
        if (participant is null) return false;

        // Enforce at least 1 manager
        if (participant.IsManager)
        {
            var managerCount = await dbContext.Participants.CountAsync(
                x => x.TournamentId == tournamentId && x.IsManager, cancellationToken);
            if (managerCount <= 1)
                throw new InvalidOperationException("Der letzte Spielleiter kann nicht entfernt werden.");
        }

        dbContext.Participants.Remove(participant);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ─── Rounds ───

    public async Task<IReadOnlyList<TournamentRoundDto>> GetRoundsAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        return await dbContext.TournamentRounds.AsNoTracking()
            .Where(x => x.TournamentId == tournamentId)
            .OrderBy(x => x.Phase).ThenBy(x => x.RoundNumber)
            .Select(x => new TournamentRoundDto(x.Id, x.TournamentId, x.Phase.ToString(), x.RoundNumber,
                x.BaseScore, x.InMode, x.OutMode, x.GameMode.ToString(), x.Legs, x.Sets, x.MaxRounds, x.BullMode, x.BullOffMode,
                x.MatchDurationMinutes, x.PauseBetweenMatchesMinutes, x.MinPlayerPauseMinutes,
                x.BoardAssignment.ToString(), x.FixedBoardId))
            .ToListAsync(cancellationToken);
    }

    public async Task<TournamentRoundDto> SaveRoundAsync(SaveTournamentRoundRequest request, CancellationToken cancellationToken = default)
    {
        var phase = Enum.TryParse<MatchPhase>(request.Phase, true, out var p) ? p : MatchPhase.Knockout;
        var existing = await dbContext.TournamentRounds
            .FirstOrDefaultAsync(x => x.TournamentId == request.TournamentId && x.Phase == phase && x.RoundNumber == request.RoundNumber, cancellationToken);

        if (existing is null)
        {
            existing = new TournamentRound { TournamentId = request.TournamentId, Phase = phase, RoundNumber = request.RoundNumber };
            dbContext.TournamentRounds.Add(existing);
        }

        existing.BaseScore = request.BaseScore;
        existing.InMode = request.InMode;
        existing.OutMode = request.OutMode;
        if (Enum.TryParse<GameMode>(request.GameMode, true, out var gm)) existing.GameMode = gm;
        existing.Legs = request.Legs;
        existing.Sets = request.Sets;
        existing.MaxRounds = request.MaxRounds;
        existing.BullMode = request.BullMode;
        existing.BullOffMode = request.BullOffMode;
        existing.MatchDurationMinutes = request.MatchDurationMinutes;
        existing.PauseBetweenMatchesMinutes = request.PauseBetweenMatchesMinutes;
        existing.MinPlayerPauseMinutes = request.MinPlayerPauseMinutes;
        if (Enum.TryParse<BoardAssignmentMode>(request.BoardAssignment, true, out var ba)) existing.BoardAssignment = ba;
        existing.FixedBoardId = request.FixedBoardId;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new TournamentRoundDto(existing.Id, existing.TournamentId, existing.Phase.ToString(), existing.RoundNumber,
            existing.BaseScore, existing.InMode, existing.OutMode, existing.GameMode.ToString(), existing.Legs, existing.Sets, existing.MaxRounds,
            existing.BullMode, existing.BullOffMode, existing.MatchDurationMinutes, existing.PauseBetweenMatchesMinutes,
            existing.MinPlayerPauseMinutes, existing.BoardAssignment.ToString(), existing.FixedBoardId);
    }

    public async Task<bool> DeleteRoundAsync(Guid tournamentId, string phase, int roundNumber, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<MatchPhase>(phase, true, out var matchPhase))
            return false;

        // Check if any match in this round has been started or finished
        var hasActiveMatches = await dbContext.Matches.AnyAsync(m =>
            m.TournamentId == tournamentId &&
            m.Phase == matchPhase &&
            m.Round == roundNumber &&
            (m.StartedUtc != null || m.FinishedUtc != null), cancellationToken);

        if (hasActiveMatches)
            throw new InvalidOperationException("Diese Spielrunde kann nicht gelöscht werden, da bereits Matches gestartet oder abgeschlossen wurden.");

        var round = await dbContext.TournamentRounds.FirstOrDefaultAsync(x =>
            x.TournamentId == tournamentId && x.Phase == matchPhase && x.RoundNumber == roundNumber, cancellationToken);

        if (round is null) return false;

        dbContext.TournamentRounds.Remove(round);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ─── Status ───

    public async Task<TournamentDto?> UpdateStatusAsync(Guid tournamentId, string status, CancellationToken cancellationToken = default)
    {
        var tournament = await dbContext.Tournaments.FirstOrDefaultAsync(x => x.Id == tournamentId, cancellationToken);
        if (tournament is null) return null;

        if (!Enum.TryParse<TournamentStatus>(status, true, out var parsedStatus))
            throw new InvalidOperationException($"Ungültiger Status: {status}");

        tournament.Status = parsedStatus;
        await dbContext.SaveChangesAsync(cancellationToken);

        var count = await dbContext.Participants.CountAsync(x => x.TournamentId == tournamentId, cancellationToken);
        return ToDto(tournament, count);
    }

    // ─── Teams ───

    public async Task<IReadOnlyList<TeamDto>> GetTeamsAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var teams = await dbContext.Teams.AsNoTracking().Where(x => x.TournamentId == tournamentId).ToListAsync(cancellationToken);
        var members = await dbContext.Participants.AsNoTracking()
            .Where(x => x.TournamentId == tournamentId && x.TeamId != null)
            .ToListAsync(cancellationToken);

        return teams.Select(t =>
        {
            var teamMembers = members.Where(m => m.TeamId == t.Id)
                .Select(m => new ParticipantDto(m.Id, m.DisplayName, m.AccountName, m.IsAutodartsAccount, m.IsManager, m.Seed, m.GroupNumber, m.TeamId))
                .ToList();
            return new TeamDto(t.Id, t.TournamentId, t.Name, t.GroupNumber, teamMembers);
        }).ToList();
    }

    public async Task<TeamDto> CreateTeamAsync(CreateTeamRequest request, CancellationToken cancellationToken = default)
    {
        var team = new Team { TournamentId = request.TournamentId, Name = request.Name };
        dbContext.Teams.Add(team);

        var participants = await dbContext.Participants
            .Where(x => x.TournamentId == request.TournamentId && request.MemberParticipantIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        foreach (var p in participants) p.TeamId = team.Id;

        await dbContext.SaveChangesAsync(cancellationToken);

        var memberDtos = participants
            .Select(m => new ParticipantDto(m.Id, m.DisplayName, m.AccountName, m.IsAutodartsAccount, m.IsManager, m.Seed, m.GroupNumber, m.TeamId))
            .ToList();
        return new TeamDto(team.Id, team.TournamentId, team.Name, team.GroupNumber, memberDtos);
    }

    public async Task<bool> DeleteTeamAsync(Guid tournamentId, Guid teamId, CancellationToken cancellationToken = default)
    {
        var team = await dbContext.Teams.FirstOrDefaultAsync(x => x.Id == teamId && x.TournamentId == tournamentId, cancellationToken);
        if (team is null) return false;

        var members = await dbContext.Participants.Where(x => x.TeamId == teamId).ToListAsync(cancellationToken);
        foreach (var m in members) m.TeamId = null;

        dbContext.Teams.Remove(team);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ─── Scoring Criteria ───

    public async Task<IReadOnlyList<ScoringCriterionDto>> GetScoringCriteriaAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        return await dbContext.ScoringCriteria.AsNoTracking()
            .Where(x => x.TournamentId == tournamentId)
            .OrderBy(x => x.Priority)
            .Select(x => new ScoringCriterionDto(x.Id, x.Type.ToString(), x.Priority, x.IsEnabled))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ScoringCriterionDto>> SaveScoringCriteriaAsync(SaveScoringCriteriaRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.ScoringCriteria.Where(x => x.TournamentId == request.TournamentId).ToListAsync(cancellationToken);
        dbContext.ScoringCriteria.RemoveRange(existing);

        foreach (var c in request.Criteria)
        {
            if (Enum.TryParse<ScoringCriterionType>(c.Type, true, out var type))
            {
                dbContext.ScoringCriteria.Add(new ScoringCriterion
                {
                    TournamentId = request.TournamentId,
                    Type = type,
                    Priority = c.Priority,
                    IsEnabled = c.IsEnabled
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetScoringCriteriaAsync(request.TournamentId, cancellationToken);
    }

    private async Task<string> GenerateUniqueJoinCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var code = new string(Enumerable.Range(0, 3)
                .Select(_ => CodeChars[Random.Shared.Next(CodeChars.Length)])
                .ToArray());
            var exists = await dbContext.Tournaments.AnyAsync(x => x.JoinCode == code, cancellationToken);
            if (!exists) return code;
        }
        throw new InvalidOperationException("Could not generate a unique join code. Too many active tournaments.");
    }
}