using ddpc.DartSuite.Application.Abstractions;
using ddpc.DartSuite.Application.Contracts.Notifications;
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
        x.GroupOrderMode.ToString(), x.ThirdPlaceMatch, x.PlayersPerTeam, x.WinPoints, x.LegFactor,
        x.IsRegistrationOpen, x.RegistrationStartUtc, x.RegistrationEndUtc,
        x.DiscordWebhookUrl, x.DiscordWebhookDisplayText, x.SeedingEnabled, x.SeedTopCount);

    /// <summary>Evaluates time-based registration and auto-toggles IsRegistrationOpen.</summary>
    private static bool EvaluateRegistrationState(Tournament t)
    {
        var now = DateTimeOffset.UtcNow;
        var changed = false;

        if (t.RegistrationStartUtc.HasValue && !t.IsRegistrationOpen && now >= t.RegistrationStartUtc.Value
            && (!t.RegistrationEndUtc.HasValue || now <= t.RegistrationEndUtc.Value))
        {
            t.IsRegistrationOpen = true;
            changed = true;
        }

        if (t.RegistrationEndUtc.HasValue && t.IsRegistrationOpen && now > t.RegistrationEndUtc.Value)
        {
            t.IsRegistrationOpen = false;
            changed = true;
        }

        return changed;
    }

    private async Task<bool> HasProgressedMatchesAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        return await dbContext.Matches.AsNoTracking().AnyAsync(
            x => x.TournamentId == tournamentId
                && x.Status != MatchStatus.WalkOver
                && (x.StartedUtc != null || x.FinishedUtc != null),
            cancellationToken);
    }

    private async Task EnsureTournamentStructureEditableAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var tournament = await dbContext.Tournaments.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tournamentId, cancellationToken);

        if (tournament is null)
            throw new InvalidOperationException("Turnier nicht gefunden.");

        if (tournament.IsLocked)
            throw new InvalidOperationException("Das Turnier ist gesperrt. Bitte entsperren Sie es, bevor Sie Änderungen vornehmen.");

        if (await HasProgressedMatchesAsync(tournamentId, cancellationToken))
            throw new InvalidOperationException("Die Turnierstruktur ist gesperrt, da bereits Matches gestartet oder beendet wurden.");
    }

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
        var tournament = await dbContext.Tournaments.FirstOrDefaultAsync(x => x.Id == tournamentId, cancellationToken);
        if (tournament is null) return null;
        if (EvaluateRegistrationState(tournament))
            await dbContext.SaveChangesAsync(cancellationToken);
        var count = await dbContext.Participants.CountAsync(x => x.TournamentId == tournamentId, cancellationToken);
        return ToDto(tournament, count);
    }

    public async Task<TournamentDto?> GetTournamentByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var normalizedCode = code.ToUpperInvariant();
        var tournament = await dbContext.Tournaments.FirstOrDefaultAsync(x => x.JoinCode == normalizedCode, cancellationToken);
        if (tournament is null) return null;
        if (EvaluateRegistrationState(tournament))
            await dbContext.SaveChangesAsync(cancellationToken);
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
        var requestedGroupMode = Enum.TryParse<GroupMode>(request.GroupMode, true, out var gm) ? gm : tournament.GroupMode;
        var requestedGroupDrawMode = Enum.TryParse<GroupDrawMode>(request.GroupDrawMode, true, out var gd) ? gd : tournament.GroupDrawMode;
        var requestedPlanningVariant = Enum.TryParse<PlanningVariant>(request.PlanningVariant, true, out var pv) ? pv : tournament.PlanningVariant;
        var requestedGroupOrderMode = Enum.TryParse<GroupOrderMode>(request.GroupOrderMode, true, out var go) ? go : tournament.GroupOrderMode;

        var requestChangesStructure =
            tournament.Mode != mode
            || tournament.TeamplayEnabled != request.TeamplayEnabled
            || tournament.GroupCount != request.GroupCount
            || tournament.PlayoffAdvancers != request.PlayoffAdvancers
            || tournament.KnockoutsPerRound != request.KnockoutsPerRound
            || tournament.MatchesPerOpponent != request.MatchesPerOpponent
            || tournament.GroupMode != requestedGroupMode
            || tournament.GroupDrawMode != requestedGroupDrawMode
            || tournament.PlanningVariant != requestedPlanningVariant
            || tournament.GroupOrderMode != requestedGroupOrderMode
            || tournament.ThirdPlaceMatch != request.ThirdPlaceMatch
            || tournament.PlayersPerTeam != request.PlayersPerTeam
            || tournament.SeedingEnabled != request.SeedingEnabled
            || tournament.SeedTopCount != request.SeedTopCount;

        if (requestChangesStructure && await HasProgressedMatchesAsync(request.Id, cancellationToken))
            throw new InvalidOperationException("Die Turnierstruktur kann nicht mehr geändert werden, da bereits Matches gestartet oder beendet wurden.");

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
        tournament.GroupMode = requestedGroupMode;
        tournament.GroupDrawMode = requestedGroupDrawMode;
        tournament.PlanningVariant = requestedPlanningVariant;
        tournament.GroupOrderMode = requestedGroupOrderMode;

        // KO settings
        tournament.ThirdPlaceMatch = request.ThirdPlaceMatch;
        tournament.PlayersPerTeam = request.PlayersPerTeam;
        tournament.WinPoints = request.WinPoints;
        tournament.LegFactor = request.LegFactor;
        tournament.AreGameModesLocked = request.AreGameModesLocked;

        // Registration settings
        tournament.IsRegistrationOpen = request.IsRegistrationOpen;
        tournament.RegistrationStartUtc = request.RegistrationStartUtc?.ToUniversalTime();
        tournament.RegistrationEndUtc = request.RegistrationEndUtc?.ToUniversalTime();

        // Discord webhook & seeding
        tournament.DiscordWebhookUrl = request.DiscordWebhookUrl;
        tournament.DiscordWebhookDisplayText = request.DiscordWebhookDisplayText;
        tournament.SeedingEnabled = request.SeedingEnabled;

        var seedingCandidateQuery = dbContext.Participants
            .Where(x => x.TournamentId == tournament.Id)
            .AsQueryable();

        if (request.TeamplayEnabled)
            seedingCandidateQuery = seedingCandidateQuery.Where(x => x.Type == ParticipantType.TeamMember);

        var seedingCandidateCount = await seedingCandidateQuery.CountAsync(cancellationToken);
        tournament.SeedTopCount = Math.Clamp(request.SeedTopCount, 0, Math.Max(0, seedingCandidateCount));

        if (request.TeamplayEnabled)
        {
            var nonTeamParticipants = await dbContext.Participants
                .Where(x => x.TournamentId == tournament.Id && x.Type != ParticipantType.TeamMember)
                .ToListAsync(cancellationToken);

            foreach (var participant in nonTeamParticipants)
            {
                participant.Seed = 0;
                participant.SeedPot = 0;
            }
        }
        else
        {
            // Teamplay deaktiviert: alle Teams und TeamMember-Participants entfernen,
            // verbleibende Spieler-Teilnehmer auf Type=Spieler, TeamId=null zurücksetzen.
            var existingTeams = await dbContext.Teams
                .Where(x => x.TournamentId == tournament.Id)
                .ToListAsync(cancellationToken);
            dbContext.Teams.RemoveRange(existingTeams);

            var teamMemberParticipants = await dbContext.Participants
                .Where(x => x.TournamentId == tournament.Id && x.Type == ParticipantType.TeamMember)
                .ToListAsync(cancellationToken);
            dbContext.Participants.RemoveRange(teamMemberParticipants);

            var assignedParticipants = await dbContext.Participants
                .Where(x => x.TournamentId == tournament.Id && x.TeamId != null)
                .ToListAsync(cancellationToken);
            foreach (var p in assignedParticipants)
            {
                p.TeamId = null;
                p.Type = ParticipantType.Spieler;
            }
        }

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
                .Select(x => new ParticipantDto(x.Id, x.DisplayName, x.AccountName, x.IsAutodartsAccount, x.IsManager, x.Seed, x.SeedPot, x.GroupNumber, x.TeamId, x.NotificationPreference.ToString(), x.Type.ToString()))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ParticipantDto>> SearchParticipantsAsync(string query, CancellationToken cancellationToken = default)
    {
        var lowerQuery = query.ToLowerInvariant();
        var results = await dbContext.Participants.AsNoTracking()
            .Where(x => x.DisplayName.ToLower().Contains(lowerQuery) || x.AccountName.ToLower().Contains(lowerQuery))
            .ToListAsync(cancellationToken);

        // Deduplicate by AccountName, preferring the most recent entry
        return results
            .GroupBy(x => x.AccountName.ToLowerInvariant())
            .Select(g => g.First())
            .Select(x => new ParticipantDto(x.Id, x.DisplayName, x.AccountName, x.IsAutodartsAccount, x.IsManager, x.Seed, x.SeedPot, x.GroupNumber, x.TeamId, x.NotificationPreference.ToString(), x.Type.ToString()))
            .Take(20)
            .ToList();
    }

    public async Task<ParticipantDto> AddParticipantAsync(AddParticipantRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureTournamentStructureEditableAsync(request.TournamentId, cancellationToken);

        var tournament = await dbContext.Tournaments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.TournamentId, cancellationToken)
            ?? throw new InvalidOperationException("Tournament not found.");

        var exists = await dbContext.Participants.AnyAsync(
            x => x.TournamentId == request.TournamentId && x.AccountName == request.AccountName,
            cancellationToken);
        if (exists) throw new InvalidOperationException("Participant already exists.");

        var participantType = ParseParticipantTypeOrDefault(request.Type, ParticipantType.Spieler);
        if (participantType == ParticipantType.TeamMember)
            throw new InvalidOperationException("Team-Teilnehmer werden automatisch ueber die Teamverwaltung erstellt.");

        var participantSeed = tournament.TeamplayEnabled && participantType != ParticipantType.TeamMember
            ? 0
            : request.Seed;

        var participant = new Participant
        {
            TournamentId = request.TournamentId,
            DisplayName = request.DisplayName,
            AccountName = request.AccountName,
            IsAutodartsAccount = request.IsAutodartsAccount,
            IsManager = request.IsManager,
            Seed = participantSeed,
            Type = participantType
        };

        dbContext.Participants.Add(participant);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new ParticipantDto(participant.Id, participant.DisplayName, participant.AccountName, participant.IsAutodartsAccount, participant.IsManager, participant.Seed, participant.SeedPot, participant.GroupNumber, participant.TeamId, participant.NotificationPreference.ToString(), participant.Type.ToString());
    }

    public async Task<ParticipantDto?> UpdateParticipantAsync(UpdateParticipantRequest request, CancellationToken cancellationToken = default)
    {
        var tournament = await dbContext.Tournaments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.TournamentId, cancellationToken)
            ?? throw new InvalidOperationException("Tournament not found.");

        var participant = await dbContext.Participants
            .FirstOrDefaultAsync(x => x.Id == request.ParticipantId && x.TournamentId == request.TournamentId, cancellationToken);
        if (participant is null) return null;

        var participantType = ParseParticipantTypeOrDefault(request.Type, participant.Type);

        if (participant.Type == ParticipantType.TeamMember && participantType != ParticipantType.TeamMember)
            throw new InvalidOperationException("Team-Teilnehmer koennen nicht in Spieler umgewandelt werden.");

        if (participant.Type != ParticipantType.TeamMember && participantType == ParticipantType.TeamMember)
            throw new InvalidOperationException("Spieler koennen nicht direkt zu Team-Teilnehmern werden. Bitte Teamverwaltung verwenden.");

        var participantSeed = tournament.TeamplayEnabled && participantType != ParticipantType.TeamMember
            ? 0
            : request.Seed;

        if (tournament.TeamplayEnabled && participantType != ParticipantType.TeamMember && request.GroupNumber.HasValue)
            throw new InvalidOperationException("Im Teamplay dürfen nur Team-Teilnehmer (TT) in der Auslosung geführt werden.");

        var changesStructure = participant.GroupNumber != request.GroupNumber
            || participant.Seed != participantSeed
            || participant.SeedPot != request.SeedPot
            || participant.Type != participantType;

        if (changesStructure)
            await EnsureTournamentStructureEditableAsync(request.TournamentId, cancellationToken);

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
        participant.Seed = participantSeed;
        participant.SeedPot = request.SeedPot;
        participant.GroupNumber = request.GroupNumber;
        participant.Type = participantType;

        await dbContext.SaveChangesAsync(cancellationToken);
            return new ParticipantDto(participant.Id, participant.DisplayName, participant.AccountName, participant.IsAutodartsAccount, participant.IsManager, participant.Seed, participant.SeedPot, participant.GroupNumber, participant.TeamId, participant.NotificationPreference.ToString(), participant.Type.ToString());
    }

    public async Task<bool> RemoveParticipantAsync(Guid tournamentId, Guid participantId, CancellationToken cancellationToken = default)
    {
        await EnsureTournamentStructureEditableAsync(tournamentId, cancellationToken);

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

        // Wenn der Teilnehmer einem Team angehört, Team und TeamMember-Participant bereinigen.
        if (participant.TeamId.HasValue)
        {
            var teamId = participant.TeamId.Value;

            var teamMemberParticipants = await dbContext.Participants
                .Where(x => x.TournamentId == tournamentId && x.TeamId == teamId && x.Type == ParticipantType.TeamMember)
                .ToListAsync(cancellationToken);
            dbContext.Participants.RemoveRange(teamMemberParticipants);

            var remainingMembers = await dbContext.Participants
                .Where(x => x.TournamentId == tournamentId && x.TeamId == teamId && x.Type != ParticipantType.TeamMember && x.Id != participant.Id)
                .ToListAsync(cancellationToken);
            foreach (var m in remainingMembers)
            {
                m.TeamId = null;
                m.Type = ParticipantType.Spieler;
            }

            var team = await dbContext.Teams.FirstOrDefaultAsync(x => x.Id == teamId && x.TournamentId == tournamentId, cancellationToken);
            if (team is not null)
                dbContext.Teams.Remove(team);
        }

        dbContext.Participants.Remove(participant);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<ParticipantDto>> AssignSeedPotsAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        await EnsureTournamentStructureEditableAsync(tournamentId, cancellationToken);

        var tournament = await dbContext.Tournaments.FindAsync([tournamentId], cancellationToken);
        if (tournament is null) return [];

        var participantsQuery = dbContext.Participants
            .Where(x => x.TournamentId == tournamentId)
            .AsQueryable();

        if (tournament.TeamplayEnabled)
            participantsQuery = participantsQuery.Where(x => x.Type == ParticipantType.TeamMember);

        var participants = await participantsQuery.ToListAsync(cancellationToken);

        if (participants.Count == 0 || tournament.GroupCount < 1) return [];

        // Determine order for pot filling:
        // 1) ranked players by seed
        // 2) unranked players randomized
        var ranked = participants
            .Where(x => tournament.SeedingEnabled && tournament.SeedTopCount > 0 && x.Seed > 0 && x.Seed <= tournament.SeedTopCount)
            .OrderBy(x => x.Seed)
            .ToList();

        var unranked = participants
            .Except(ranked)
            .OrderBy(_ => Random.Shared.Next())
            .ToList();

        var ordered = ranked.Concat(unranked).ToList();

        // Determine pot size = number of groups (each pot fills one slot per group)
        var potSize = tournament.GroupCount;
        var potNumber = 1;
        for (var i = 0; i < ordered.Count; i++)
        {
            if (i > 0 && i % potSize == 0) potNumber++;
            ordered[i].SeedPot = potNumber;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return participants
              .Select(x => new ParticipantDto(x.Id, x.DisplayName, x.AccountName, x.IsAutodartsAccount, x.IsManager, x.Seed, x.SeedPot, x.GroupNumber, x.TeamId, x.NotificationPreference.ToString(), x.Type.ToString()))
            .ToList();
    }

    private static ParticipantType ParseParticipantTypeOrDefault(string? rawType, ParticipantType fallback)
    {
        if (string.IsNullOrWhiteSpace(rawType))
            return fallback;

        return Enum.TryParse<ParticipantType>(rawType, true, out var parsed)
            ? parsed
            : fallback;
    }

    // ─── Rounds ───

    public async Task<IReadOnlyList<TournamentRoundDto>> GetRoundsAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        return await dbContext.TournamentRounds.AsNoTracking()
            .Where(x => x.TournamentId == tournamentId)
            .OrderBy(x => x.Phase).ThenBy(x => x.RoundNumber)
            .Select(x => new TournamentRoundDto(x.Id, x.TournamentId, x.Phase.ToString(), x.RoundNumber,
                x.BaseScore, x.InMode, x.OutMode, x.GameMode.ToString(), x.Legs, x.Sets, x.MaxRounds, x.BullMode, x.BullOffMode,
                x.LegDurationSeconds, x.PauseBetweenMatchesMinutes, x.MinPlayerPauseMinutes,
                x.BoardAssignment.ToString(), x.FixedBoardId))
            .ToListAsync(cancellationToken);
    }

    public async Task<TournamentRoundDto> SaveRoundAsync(SaveTournamentRoundRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureTournamentStructureEditableAsync(request.TournamentId, cancellationToken);

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
        existing.LegDurationSeconds = request.LegDurationSeconds;
        existing.PauseBetweenMatchesMinutes = request.PauseBetweenMatchesMinutes;
        existing.MinPlayerPauseMinutes = request.MinPlayerPauseMinutes;
        if (Enum.TryParse<BoardAssignmentMode>(request.BoardAssignment, true, out var ba)) existing.BoardAssignment = ba;
        existing.FixedBoardId = request.FixedBoardId;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new TournamentRoundDto(existing.Id, existing.TournamentId, existing.Phase.ToString(), existing.RoundNumber,
            existing.BaseScore, existing.InMode, existing.OutMode, existing.GameMode.ToString(), existing.Legs, existing.Sets, existing.MaxRounds,
            existing.BullMode, existing.BullOffMode, existing.LegDurationSeconds, existing.PauseBetweenMatchesMinutes,
            existing.MinPlayerPauseMinutes, existing.BoardAssignment.ToString(), existing.FixedBoardId);
    }

    public async Task<bool> DeleteRoundAsync(Guid tournamentId, string phase, int roundNumber, CancellationToken cancellationToken = default)
    {
        await EnsureTournamentStructureEditableAsync(tournamentId, cancellationToken);

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

        if (!Enum.TryParse<TournamentStatus>(status, true, out var newStatus))
            throw new InvalidOperationException($"Ungültiger Status: {status}");

        var oldStatus = tournament.Status;

        // Downgrade cascading logic
        if (newStatus < oldStatus)
        {
            // Gestartet → Geplant: reset all matches
            if (oldStatus >= TournamentStatus.Gestartet && newStatus <= TournamentStatus.Geplant)
            {
                var matches = await dbContext.Matches
                    .Where(x => x.TournamentId == tournamentId && x.Status != MatchStatus.WalkOver)
                    .ToListAsync(cancellationToken);
                foreach (var m in matches)
                {
                    m.HomeLegs = 0;
                    m.AwayLegs = 0;
                    m.HomeSets = 0;
                    m.AwaySets = 0;
                    m.WinnerParticipantId = null;
                    m.StartedUtc = null;
                    m.FinishedUtc = null;
                    m.ExternalMatchId = null;
                    m.RecomputeStatus();
                }
            }

            // Geplant → Erstellt: delete rounds, matches, schedule
            if (oldStatus >= TournamentStatus.Geplant && newStatus <= TournamentStatus.Erstellt)
            {
                var rounds = await dbContext.TournamentRounds
                    .Where(x => x.TournamentId == tournamentId)
                    .ToListAsync(cancellationToken);
                dbContext.TournamentRounds.RemoveRange(rounds);

                var matches = await dbContext.Matches
                    .Where(x => x.TournamentId == tournamentId)
                    .ToListAsync(cancellationToken);
                dbContext.Matches.RemoveRange(matches);
            }
        }

        tournament.Status = newStatus;
        await dbContext.SaveChangesAsync(cancellationToken);

        var count = await dbContext.Participants.CountAsync(x => x.TournamentId == tournamentId, cancellationToken);
        return ToDto(tournament, count);
    }

    // ─── Delete Tournament ───

    public async Task<bool> DeleteTournamentAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var tournament = await dbContext.Tournaments.FirstOrDefaultAsync(x => x.Id == tournamentId, cancellationToken);
        if (tournament is null) return false;

        // Only allow deletion when no matches have been played
        var hasPlayedMatches = await dbContext.Matches.AnyAsync(
            x => x.TournamentId == tournamentId && (x.StartedUtc != null || x.FinishedUtc != null) && x.Status != MatchStatus.WalkOver,
            cancellationToken);
        if (hasPlayedMatches)
            throw new InvalidOperationException("Das Turnier kann nicht gelöscht werden, da bereits Matches gespielt wurden. Verwenden Sie stattdessen 'Abbrechen'.");

        // Remove all related data
        var matches = await dbContext.Matches.Where(x => x.TournamentId == tournamentId).ToListAsync(cancellationToken);
        dbContext.Matches.RemoveRange(matches);

        var rounds = await dbContext.TournamentRounds.Where(x => x.TournamentId == tournamentId).ToListAsync(cancellationToken);
        dbContext.TournamentRounds.RemoveRange(rounds);

        var teams = await dbContext.Teams.Where(x => x.TournamentId == tournamentId).ToListAsync(cancellationToken);
        dbContext.Teams.RemoveRange(teams);

        var scoring = await dbContext.ScoringCriteria.Where(x => x.TournamentId == tournamentId).ToListAsync(cancellationToken);
        dbContext.ScoringCriteria.RemoveRange(scoring);

        var participants = await dbContext.Participants.Where(x => x.TournamentId == tournamentId).ToListAsync(cancellationToken);
        dbContext.Participants.RemoveRange(participants);

        dbContext.Tournaments.Remove(tournament);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ─── Teams ───

    public async Task<IReadOnlyList<TeamDto>> GetTeamsAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var teams = await dbContext.Teams.AsNoTracking().Where(x => x.TournamentId == tournamentId).ToListAsync(cancellationToken);
        var members = await dbContext.Participants.AsNoTracking()
            .Where(x => x.TournamentId == tournamentId && x.TeamId != null && x.Type != ParticipantType.TeamMember)
            .ToListAsync(cancellationToken);

        return teams.Select(t =>
        {
            var teamMembers = members.Where(m => m.TeamId == t.Id)
                .Select(m => new ParticipantDto(m.Id, m.DisplayName, m.AccountName, m.IsAutodartsAccount, m.IsManager, m.Seed, m.SeedPot, m.GroupNumber, m.TeamId, m.NotificationPreference.ToString(), m.Type.ToString()))
                .ToList();
            return new TeamDto(t.Id, t.TournamentId, t.Name, t.GroupNumber, teamMembers);
        }).ToList();
    }

    public async Task<TeamDto> CreateTeamAsync(CreateTeamRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureTournamentStructureEditableAsync(request.TournamentId, cancellationToken);

        var participants = await dbContext.Participants
            .Where(x => x.TournamentId == request.TournamentId && request.MemberParticipantIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        // Auto-generate team name from player names if not provided
        var teamName = string.IsNullOrWhiteSpace(request.Name)
            ? string.Join("/", participants.Select(p => p.DisplayName))
            : request.Name;

        var team = new Team { TournamentId = request.TournamentId, Name = teamName };
        dbContext.Teams.Add(team);

        foreach (var p in participants)
        {
            p.TeamId = team.Id;
            p.Type = ParticipantType.Spieler;
        }

        var teamParticipant = new Participant
        {
            TournamentId = request.TournamentId,
            DisplayName = teamName,
            AccountName = team.Id.ToString(),
            IsAutodartsAccount = false,
            IsManager = false,
            TeamId = team.Id,
            Type = ParticipantType.TeamMember
        };
        dbContext.Participants.Add(teamParticipant);

        await dbContext.SaveChangesAsync(cancellationToken);

        var memberDtos = participants
            .Select(m => new ParticipantDto(m.Id, m.DisplayName, m.AccountName, m.IsAutodartsAccount, m.IsManager, m.Seed, m.SeedPot, m.GroupNumber, m.TeamId, m.NotificationPreference.ToString(), m.Type.ToString()))
            .ToList();
        return new TeamDto(team.Id, team.TournamentId, team.Name, team.GroupNumber, memberDtos);
    }

    public async Task<IReadOnlyList<TeamDto>> SaveTeamsAsync(SaveTeamsRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureTournamentStructureEditableAsync(request.TournamentId, cancellationToken);

        var tournament = await dbContext.Tournaments.FirstOrDefaultAsync(x => x.Id == request.TournamentId, cancellationToken);
        if (tournament is null) return [];

        var participants = await dbContext.Participants
            .Where(x => x.TournamentId == request.TournamentId && x.Type != ParticipantType.TeamMember)
            .ToListAsync(cancellationToken);
        var participantsById = participants.ToDictionary(x => x.Id);

        var existingTeamParticipants = await dbContext.Participants
            .Where(x => x.TournamentId == request.TournamentId && x.Type == ParticipantType.TeamMember)
            .ToListAsync(cancellationToken);

        var requestedTeams = request.Teams
            .Where(x => x.MemberParticipantIds.Count > 0)
            .ToList();

        var duplicateMember = requestedTeams
            .SelectMany(x => x.MemberParticipantIds)
            .GroupBy(x => x)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicateMember is not null)
            throw new InvalidOperationException("Ein Teilnehmer ist mehrfach in verschiedenen Teams zugewiesen.");

        var hasInvalidMembers = requestedTeams
            .SelectMany(x => x.MemberParticipantIds)
            .Any(id => !participantsById.ContainsKey(id));
        if (hasInvalidMembers)
            throw new InvalidOperationException("Mindestens ein Teammitglied gehört nicht zum Turnier.");

        if (tournament.TeamplayEnabled)
        {
            var playersPerTeam = Math.Max(1, tournament.PlayersPerTeam);
            if (participants.Count % playersPerTeam != 0)
                throw new InvalidOperationException("Die Teilnehmeranzahl ist nicht durch die Teamgröße teilbar.");

            if (requestedTeams.Any(x => x.MemberParticipantIds.Count > playersPerTeam))
                throw new InvalidOperationException("Ein Team überschreitet die konfigurierte Spieleranzahl.");
        }

        var existingTeams = await dbContext.Teams.Where(x => x.TournamentId == request.TournamentId).ToListAsync(cancellationToken);
        dbContext.Teams.RemoveRange(existingTeams);
        dbContext.Participants.RemoveRange(existingTeamParticipants);
        foreach (var participant in participants)
            {
                participant.TeamId = null;
                participant.Type = ParticipantType.Spieler;
                participant.Seed = 0;
            }

        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var teamRequest in requestedTeams)
        {
            var members = teamRequest.MemberParticipantIds
                .Select(id => participantsById[id])
                .ToList();

            var rawName = string.IsNullOrWhiteSpace(teamRequest.Name)
                ? string.Join("/", members.Select(m => m.DisplayName))
                : teamRequest.Name.Trim();
            var teamName = EnsureUniqueTeamName(rawName, usedNames);

            var team = new Team
            {
                TournamentId = request.TournamentId,
                Name = teamName
            };

            dbContext.Teams.Add(team);
            foreach (var member in members)
                {
                    member.TeamId = team.Id;
                    member.Type = ParticipantType.Spieler;
                }

            dbContext.Participants.Add(new Participant
            {
                TournamentId = request.TournamentId,
                DisplayName = teamName,
                AccountName = team.Id.ToString(),
                IsAutodartsAccount = false,
                IsManager = false,
                TeamId = team.Id,
                Type = ParticipantType.TeamMember
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetTeamsAsync(request.TournamentId, cancellationToken);
    }

    public async Task<bool> DeleteTeamAsync(Guid tournamentId, Guid teamId, CancellationToken cancellationToken = default)
    {
        await EnsureTournamentStructureEditableAsync(tournamentId, cancellationToken);

        var team = await dbContext.Teams.FirstOrDefaultAsync(x => x.Id == teamId && x.TournamentId == tournamentId, cancellationToken);
        if (team is null) return false;

        var members = await dbContext.Participants.Where(x => x.TeamId == teamId && x.Type != ParticipantType.TeamMember).ToListAsync(cancellationToken);
        foreach (var m in members)
        {
            m.TeamId = null;
            m.Type = ParticipantType.Spieler;
        }

        var teamParticipants = await dbContext.Participants
            .Where(x => x.TournamentId == tournamentId && x.TeamId == teamId && x.Type == ParticipantType.TeamMember)
            .ToListAsync(cancellationToken);
        dbContext.Participants.RemoveRange(teamParticipants);

        dbContext.Teams.Remove(team);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string EnsureUniqueTeamName(string requestedName, ISet<string> usedNames)
    {
        var baseName = string.IsNullOrWhiteSpace(requestedName) ? "Team" : requestedName.Trim();
        var candidate = baseName;
        var suffix = 2;

        while (!usedNames.Add(candidate))
        {
            candidate = $"{baseName} ({suffix})";
            suffix++;
        }

        return candidate;
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
        await EnsureTournamentStructureEditableAsync(request.TournamentId, cancellationToken);

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

    // ─── Notifications (#14) ───

    public async Task<IReadOnlyList<NotificationSubscriptionDto>> GetNotificationSubscriptionsAsync(Guid tournamentId, string userAccountName, CancellationToken cancellationToken = default)
    {
        return await dbContext.NotificationSubscriptions.AsNoTracking()
            .Where(x => x.TournamentId == tournamentId && x.UserAccountName == userAccountName)
            .Select(x => new NotificationSubscriptionDto(x.Id, x.TournamentId, x.UserAccountName, x.Endpoint, x.P256dh, x.Auth, x.NotificationPreference.ToString(), x.CreatedUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<NotificationSubscriptionDto> SubscribeNotificationsAsync(CreateNotificationSubscriptionRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.NotificationSubscriptions
            .FirstOrDefaultAsync(x => x.TournamentId == request.TournamentId && x.UserAccountName == request.UserAccountName && x.Endpoint == request.Endpoint, cancellationToken);

        if (existing is not null)
        {
            existing.P256dh = request.P256dh;
            existing.Auth = request.Auth;
            existing.NotificationPreference = Enum.TryParse<NotificationPreference>(request.NotificationPreference, true, out var np) ? np : NotificationPreference.OwnMatches;
        }
        else
        {
            existing = new NotificationSubscription
            {
                TournamentId = request.TournamentId,
                UserAccountName = request.UserAccountName,
                Endpoint = request.Endpoint,
                P256dh = request.P256dh,
                Auth = request.Auth,
                NotificationPreference = Enum.TryParse<NotificationPreference>(request.NotificationPreference, true, out var np2) ? np2 : NotificationPreference.OwnMatches
            };
            dbContext.NotificationSubscriptions.Add(existing);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new NotificationSubscriptionDto(existing.Id, existing.TournamentId, existing.UserAccountName, existing.Endpoint, existing.P256dh, existing.Auth, existing.NotificationPreference.ToString(), existing.CreatedUtc);
    }

    public async Task<bool> UnsubscribeNotificationsAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        var sub = await dbContext.NotificationSubscriptions.FirstOrDefaultAsync(x => x.Id == subscriptionId, cancellationToken);
        if (sub is null) return false;

        dbContext.NotificationSubscriptions.Remove(sub);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ─── View Preferences (#15) ───

    public async Task<UserViewPreferenceDto?> GetUserViewPreferenceAsync(string userAccountName, string viewContext, CancellationToken cancellationToken = default)
    {
        var pref = await dbContext.UserViewPreferences.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserAccountName == userAccountName && x.ViewContext == viewContext, cancellationToken);
        return pref is null ? null : new UserViewPreferenceDto(pref.Id, pref.UserAccountName, pref.ViewContext, pref.SettingsJson);
    }

    public async Task<UserViewPreferenceDto> SaveUserViewPreferenceAsync(string userAccountName, string viewContext, string settingsJson, CancellationToken cancellationToken = default)
    {
        var pref = await dbContext.UserViewPreferences
            .FirstOrDefaultAsync(x => x.UserAccountName == userAccountName && x.ViewContext == viewContext, cancellationToken);

        if (pref is null)
        {
            pref = new UserViewPreference
            {
                UserAccountName = userAccountName,
                ViewContext = viewContext,
                SettingsJson = settingsJson
            };
            dbContext.UserViewPreferences.Add(pref);
        }
        else
        {
            pref.SettingsJson = settingsJson;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new UserViewPreferenceDto(pref.Id, pref.UserAccountName, pref.ViewContext, pref.SettingsJson);
    }
}
