using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Constants;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        private static readonly TimeOnly ExtensionLockTime = new(17, 0);
        private static readonly TimeOnly DefaultBranchCloseTime = new(20, 0);

        public async Task<WorkSlotDynamicExtensionResponseDto> ProcessDynamicExtensionAsync(
            long workSlotId,
            WorkSlotDynamicExtensionRequestDto dto,
            long? actorUserId = null)
        {
            if (dto == null)
                throw new InvalidOperationException("Thi\u1EBFu d\u1EEF li\u1EC7u x\u1EED l\u00FD gia h\u1EA1n.");

            var action = NormalizeDynamicExtensionAction(dto.Action);
            var now = dto.PerformedAt ?? _clock.LocalNow;

            var slot = await _context.WorkSlots
                .FirstOrDefaultAsync(s => s.Id == workSlotId);

            if (slot == null)
                throw new KeyNotFoundException("Kh\u00F4ng t\u00ECm th\u1EA5y ca s\u1EEDa ch\u1EEFa c\u1EA7n x\u1EED l\u00FD.");
            if (IsClosedWorkSlotStatus(slot.Status))
                throw new InvalidOperationException("Ca s\u1EEDa ch\u1EEFa \u0111\u00E3 k\u1EBFt th\u00FAc, kh\u00F4ng th\u1EC3 gia h\u1EA1n.");

            var response = new WorkSlotDynamicExtensionResponseDto
            {
                WorkSlotId = slot.Id,
                Action = action,
                ExecutedAt = now,
                PreviousEndTime = slot.EndTime,
                NewEndTime = slot.EndTime
            };

            if (action == "COMPLETE")
            {
                slot.ActualReleaseTime = now;
                if (now > slot.StartTime)
                {
                    slot.EndTime = now;
                }
                slot.Status = nameof(WorkSlotStatus.COMPLETED);
                await _context.SaveChangesAsync();

                response.IsSuccessful = true;
                response.NewEndTime = slot.EndTime;
                response.Message = "Khoang \u0111\u00E3 \u0111\u01B0\u1EE3c gi\u1EA3i ph\u00F3ng ngay sau khi ho\u00E0n t\u1EA5t ch\u1EA9n \u0111o\u00E1n.";
                return response;
            }

            var additionalMinutes = dto.AdditionalMinutes.GetValueOrDefault();
            if (additionalMinutes <= 0)
                throw new InvalidOperationException("S\u1ED1 ph\u00FAt gia h\u1EA1n ph\u1EA3i l\u1EDBn h\u01A1n 0.");

            var slotStepMinutes = await ResolveBranchSlotStepMinutesAsync(slot.BranchId, now);
            var closeAt = await ResolveBranchCloseTimeAsync(slot.BranchId, now);
            var extensionMinutes = RoundUpToStep(additionalMinutes + FlexibleBufferMinutes, slotStepMinutes);
            var proposedEnd = now.AddMinutes(extensionMinutes);

            if (TimeOnly.FromDateTime(now) >= ExtensionLockTime &&
                proposedEnd > closeAt &&
                !dto.SaveOvernight)
            {
                throw new InvalidOperationException("Sau 17:00 ch\u1EC9 \u0111\u01B0\u1EE3c gia h\u1EA1n v\u01B0\u1EE3t gi\u1EDD \u0111\u00F3ng c\u1EEDa khi ch\u1ECDn l\u01B0u xe qua \u0111\u00EAm.");
            }

            var effectiveEnd = proposedEnd > closeAt ? closeAt : proposedEnd;
            if (effectiveEnd <= now)
            {
                effectiveEnd = now.AddMinutes(slotStepMinutes);
            }

            var collisions = await LoadCollisionSlotsAsync(slot, now, effectiveEnd);
            if (collisions.Count > 0)
            {
                response.HasCollision = true;
                response.RequiresManualDispatch = true;
                response.NewEndTime = effectiveEnd;
                response.Message = "Ph\u00E1t hi\u1EC7n xung \u0111\u1ED9t v\u1EDBi l\u1ECBch k\u1EBF ti\u1EBFp. Vui l\u00F2ng x\u1EED l\u00FD theo b\u1EA3ng \u0111i\u1EC1u ph\u1ED1i kh\u1EA9n c\u1EA5p.";
                response.Suggestions = await BuildEmergencyDispatchSuggestionsAsync(slot, collisions, now, extensionMinutes);

                if (dto.TryBorrowDvnBay)
                {
                    var borrowSuggestion = response.Suggestions
                        .FirstOrDefault(s => string.Equals(s.Type, "BORROW_DVN", StringComparison.OrdinalIgnoreCase) && s.BayId.HasValue);

                    if (borrowSuggestion?.BayId is long borrowedBayId)
                    {
                        slot.BayId = borrowedBayId;
                        slot.EndTime = effectiveEnd;
                        slot.ActualReleaseTime = null;
                        slot.Notes = AppendWorkSlotTag(slot.Notes, $"MUON_DVN:{now:yyyyMMddHHmm}");
                        await _context.SaveChangesAsync();

                        response.IsSuccessful = true;
                        response.HasCollision = false;
                        response.RequiresManualDispatch = false;
                        response.BorrowedDvnBay = true;
                        response.BorrowedBayId = borrowedBayId;
                        response.BorrowedBayCode = borrowSuggestion.BayCode;
                        response.Message = "\u0110\u00E3 m\u01B0\u1EE3n khoang DVN \u0111\u1EC3 ti\u1EBFp t\u1EE5c s\u1EEDa ch\u1EEFa v\u00E0 tr\u00E1nh \u0111\u1EA9y l\u1ECBch kh\u00E1ch sau.";
                    }
                }

                return response;
            }

            slot.EndTime = effectiveEnd;
            slot.ActualReleaseTime = null;
            await _context.SaveChangesAsync();

            response.IsSuccessful = true;
            response.NewEndTime = slot.EndTime;
            response.Message = "Gia h\u1EA1n ca s\u1EEDa ch\u1EEFa th\u00E0nh c\u00F4ng.";

            if (proposedEnd > closeAt && dto.SaveOvernight)
            {
                var overnightSlot = await CreateOrUpdateOvernightLockFromExtensionAsync(slot, closeAt, proposedEnd, slotStepMinutes);
                if (overnightSlot != null)
                {
                    response.OvernightLockCreated = true;
                    response.OvernightLockSlotId = overnightSlot.Id;
                }
            }

            return response;
        }

        private async Task<int> ResolveBranchSlotStepMinutesAsync(long branchId, DateTime at)
        {
            var opHour = await _context.BranchOperatingHours
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.BranchId == branchId && h.DayOfWeek == (int)at.DayOfWeek);

            return ResolveSlotStepMinutes(opHour?.SlotDurationMinutes);
        }

        private async Task<DateTime> ResolveBranchCloseTimeAsync(long branchId, DateTime at)
        {
            var opHour = await _context.BranchOperatingHours
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.BranchId == branchId && h.DayOfWeek == (int)at.DayOfWeek);
            var closeTime = opHour?.CloseTime ?? DefaultBranchCloseTime;
            return at.Date.Add(closeTime.ToTimeSpan());
        }

        private async Task<List<WorkSlot>> LoadCollisionSlotsAsync(WorkSlot targetSlot, DateTime now, DateTime proposedEnd)
        {
            var candidates = await _context.WorkSlots
                .AsNoTracking()
                .Where(s => s.BranchId == targetSlot.BranchId
                    && s.BayId == targetSlot.BayId
                    && s.Id != targetSlot.Id
                    && s.StartTime < proposedEnd
                    && (s.ActualReleaseTime == null || s.ActualReleaseTime > now)
                    && s.Status != nameof(WorkSlotStatus.CANCELED)
                    && s.Status != nameof(WorkSlotStatus.COMPLETED))
                .OrderBy(s => s.StartTime)
                .ToListAsync();

            return candidates
                .Where(s => ResolveEffectiveSlotEnd(s) > now && !IsClosedWorkSlotStatus(s.Status))
                .ToList();
        }

        private async Task<List<DynamicDispatchSuggestionDto>> BuildEmergencyDispatchSuggestionsAsync(
            WorkSlot currentSlot,
            IReadOnlyList<WorkSlot> collisions,
            DateTime now,
            int extensionMinutes)
        {
            var suggestions = new List<DynamicDispatchSuggestionDto>();

            var bays = await _context.Bays
                .AsNoTracking()
                .Where(b => b.BranchId == currentSlot.BranchId && b.IsActive == true && b.IsDeleted != true)
                .OrderBy(b => b.Code)
                .ToListAsync();

            var timelineEnd = now.AddHours(12);
            var openSlots = await _context.WorkSlots
                .AsNoTracking()
                .Where(s => s.BranchId == currentSlot.BranchId
                    && s.StartTime < timelineEnd
                    && (s.ActualReleaseTime == null || s.ActualReleaseTime > now)
                    && s.Status != nameof(WorkSlotStatus.CANCELED)
                    && s.Status != nameof(WorkSlotStatus.COMPLETED))
                .ToListAsync();

            var currentNeedsQuick = IsQuickSlotType(currentSlot.SlotType, currentSlot.Notes);
            var preferredMoveBay = FindFirstFreeBay(
                bays,
                openSlots,
                now,
                now.AddMinutes(extensionMinutes),
                currentNeedsQuick,
                allowQuickFallbackForHeavy: false,
                currentSlot.Id,
                currentSlot.BayId);

            if (preferredMoveBay != null)
            {
                suggestions.Add(new DynamicDispatchSuggestionDto
                {
                    Type = "MOVE_CURRENT",
                    Message = $"Khoang {preferredMoveBay.Code} \u0111ang tr\u1ED1ng \u0111\u1EE7 th\u1EDDi gian, c\u00F3 th\u1EC3 chuy\u1EC3n xe hi\u1EC7n t\u1EA1i sang \u0111\u1EC3 ti\u1EBFp t\u1EE5c s\u1EEDa ch\u1EEFa.",
                    BayId = preferredMoveBay.Id,
                    BayCode = preferredMoveBay.Code,
                    AvailableFrom = now,
                    AvailableTo = now.AddMinutes(extensionMinutes)
                });
            }

            var nextSlot = collisions.OrderBy(s => s.StartTime).FirstOrDefault();
            if (nextSlot != null)
            {
                var nextDuration = Math.Max(30, (int)Math.Ceiling((nextSlot.EndTime - nextSlot.StartTime).TotalMinutes));
                var nextNeedsQuick = IsQuickSlotType(nextSlot.SlotType, nextSlot.Notes);
                var redirectBay = FindFirstFreeBay(
                    bays,
                    openSlots,
                    nextSlot.StartTime,
                    nextSlot.StartTime.AddMinutes(nextDuration),
                    nextNeedsQuick,
                    allowQuickFallbackForHeavy: true,
                    nextSlot.Id,
                    nextSlot.BayId);

                if (redirectBay != null)
                {
                    suggestions.Add(new DynamicDispatchSuggestionDto
                    {
                        Type = "REDIRECT_NEXT",
                        Message = $"C\u00F3 th\u1EC3 \u0111i\u1EC1u h\u01B0\u1EDBng l\u1ECBch k\u1EBF ti\u1EBFp sang khoang {redirectBay.Code} \u0111\u1EC3 tr\u00E1nh ch\u1EADm d\u00E2y chuy\u1EC1n.",
                        BayId = redirectBay.Id,
                        BayCode = redirectBay.Code,
                        AvailableFrom = nextSlot.StartTime,
                        AvailableTo = nextSlot.StartTime.AddMinutes(nextDuration),
                        RelatedWorkSlotId = nextSlot.Id
                    });
                }
            }

            var nextFreeOnCurrentBay = collisions
                .Select(ResolveEffectiveSlotEnd)
                .DefaultIfEmpty(now)
                .Max();

            suggestions.Add(new DynamicDispatchSuggestionDto
            {
                Type = "WAIT_FOR_NEXT_GAP",
                Message = $"N\u1EBFu kh\u00E1ch \u0111\u1ED3ng \u00FD \u0111\u1EC3 xe ch\u1EDD, c\u00F3 th\u1EC3 t\u1EA1m \u0111\u01B0a xe ra b\u00E3i v\u00E0 v\u00E0o l\u1EA1i khoang l\u00FAc {nextFreeOnCurrentBay:HH:mm}.",
                BayId = currentSlot.BayId,
                AvailableFrom = nextFreeOnCurrentBay
            });

            if (!currentNeedsQuick)
            {
                var borrowDvn = TryFindBorrowDvnSuggestion(bays, openSlots, now, extensionMinutes, currentSlot);
                if (borrowDvn != null)
                {
                    suggestions.Add(borrowDvn);
                }
            }

            return suggestions;
        }

        private DynamicDispatchSuggestionDto? TryFindBorrowDvnSuggestion(
            IReadOnlyList<Bay> bays,
            IReadOnlyList<WorkSlot> openSlots,
            DateTime now,
            int extensionMinutes,
            WorkSlot currentSlot)
        {
            var repairBays = bays.Where(IsRepairBay).ToList();
            if (repairBays.Count == 0)
            {
                return null;
            }

            var busyRepairBayIds = openSlots
                .Where(s => repairBays.Any(b => b.Id == s.BayId) && s.StartTime <= now && ResolveEffectiveSlotEnd(s) > now)
                .Select(s => s.BayId)
                .Distinct()
                .ToHashSet();

            if (busyRepairBayIds.Count < repairBays.Count)
            {
                return null;
            }

            var borrowMinutes = Math.Max(60, extensionMinutes);
            var quickBay = FindFirstFreeBay(
                bays,
                openSlots,
                now,
                now.AddMinutes(borrowMinutes),
                mustBeQuick: true,
                allowQuickFallbackForHeavy: true,
                ignoredSlotId: currentSlot.Id,
                excludedBayId: currentSlot.BayId);

            if (quickBay == null)
            {
                return null;
            }

            return new DynamicDispatchSuggestionDto
            {
                Type = "BORROW_DVN",
                Message = $"To\u00E0n b\u1ED9 khoang s\u1EEDa ch\u1EEFa \u0111ang k\u00EDn. C\u00F3 th\u1EC3 m\u01B0\u1EE3n khoang DVN {quickBay.Code} \u0111\u1EC3 gi\u1EA3i ph\u00F3ng khoang s\u1EEDa ch\u1EEFa ch\u00EDnh.",
                BayId = quickBay.Id,
                BayCode = quickBay.Code,
                AvailableFrom = now,
                AvailableTo = now.AddMinutes(borrowMinutes)
            };
        }

        private static Bay? FindFirstFreeBay(
            IReadOnlyList<Bay> bays,
            IReadOnlyList<WorkSlot> openSlots,
            DateTime windowStart,
            DateTime windowEnd,
            bool mustBeQuick,
            bool allowQuickFallbackForHeavy,
            long ignoredSlotId,
            long excludedBayId)
        {
            IEnumerable<Bay> candidates;
            if (mustBeQuick)
            {
                var quick = bays.Where(b => IsQuickBay(b) && b.Id != excludedBayId);
                var other = bays.Where(b => IsRepairBay(b) && b.Id != excludedBayId);
                candidates = quick.Concat(other);
            }
            else
            {
                var repair = bays.Where(b => IsRepairBay(b) && b.Id != excludedBayId);
                if (allowQuickFallbackForHeavy)
                {
                    candidates = repair.Concat(bays.Where(b => IsQuickBay(b) && b.Id != excludedBayId));
                }
                else
                {
                    candidates = repair;
                }
            }

            foreach (var bay in candidates)
            {
                var isBusy = openSlots.Any(s =>
                    s.BayId == bay.Id &&
                    s.Id != ignoredSlotId &&
                    s.StartTime < windowEnd &&
                    ResolveEffectiveSlotEnd(s) > windowStart &&
                    !IsClosedWorkSlotStatus(s.Status));

                if (!isBusy)
                {
                    return bay;
                }
            }

            return null;
        }

        private async Task<WorkSlot?> CreateOrUpdateOvernightLockFromExtensionAsync(
            WorkSlot sourceSlot,
            DateTime closeAt,
            DateTime proposedEnd,
            int slotStepMinutes)
        {
            var remainingMinutes = (int)Math.Ceiling((proposedEnd - closeAt).TotalMinutes);
            if (remainingMinutes <= 0)
            {
                return null;
            }

            var lockMinutes = RoundUpToStep(remainingMinutes, slotStepMinutes);
            var nextMorning = DateOnly.FromDateTime(closeAt).AddDays(1).ToDateTime(new TimeOnly(8, 0));
            var marker = BuildOvernightSourceMarker(sourceSlot.Id);

            var existingLock = await _context.WorkSlots
                .FirstOrDefaultAsync(s => s.BranchId == sourceSlot.BranchId
                    && s.SlotType == nameof(WorkSlotType.OVERNIGHT_LOCK)
                    && s.Notes != null
                    && s.Notes.Contains(marker));

            if (existingLock == null)
            {
                var lockSlot = BuildOvernightLockSlot(sourceSlot, nextMorning, lockMinutes);
                _context.WorkSlots.Add(lockSlot);
                await _context.SaveChangesAsync();
                return lockSlot;
            }

            existingLock.BayId = sourceSlot.BayId;
            existingLock.StartTime = nextMorning;
            existingLock.EndTime = nextMorning.AddMinutes(lockMinutes);
            existingLock.Status = nameof(WorkSlotStatus.LOCKED);
            existingLock.VehicleId = sourceSlot.VehicleId;
            existingLock.CustomerId = sourceSlot.CustomerId;
            await _context.SaveChangesAsync();
            return existingLock;
        }

        private static string NormalizeDynamicExtensionAction(string? action)
        {
            var value = NormalizeText(action ?? string.Empty);
            if (value is "complete" or "hoan tat")
            {
                return "COMPLETE";
            }

            if (value is "extend" or "gia han")
            {
                return "EXTEND";
            }

            throw new InvalidOperationException("H\u00E0nh \u0111\u1ED9ng kh\u00F4ng h\u1EE3p l\u1EC7. Ch\u1EC9 h\u1ED7 tr\u1EE3 COMPLETE ho\u1EB7c EXTEND.");
        }

        private static DateTime ResolveEffectiveSlotEnd(WorkSlot slot)
        {
            return slot.ActualReleaseTime ?? slot.EndTime;
        }

        private static bool IsClosedWorkSlotStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return false;
            }

            return string.Equals(status, nameof(WorkSlotStatus.CANCELED), StringComparison.OrdinalIgnoreCase)
                   || string.Equals(status, nameof(WorkSlotStatus.COMPLETED), StringComparison.OrdinalIgnoreCase)
                   || string.Equals(status, nameof(WorkSlotStatus.CANCELLED), StringComparison.OrdinalIgnoreCase);
        }

        private static string AppendWorkSlotTag(string? notes, string tag)
        {
            var marker = $"[{tag}]";
            if (string.IsNullOrWhiteSpace(notes))
            {
                return marker;
            }

            if (notes.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return notes;
            }

            return $"{notes} {marker}";
        }
    }
}
