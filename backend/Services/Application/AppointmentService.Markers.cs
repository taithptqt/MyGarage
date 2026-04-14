using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MyGarage.Constants;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        private static string AppendFlowMarker(string? notes, BookingFlowType flow)
        {
            var marker = flow == BookingFlowType.Flexible ? AppointmentNoteMarkers.FlowFlexible : AppointmentNoteMarkers.FlowFixed;
            if (string.IsNullOrWhiteSpace(notes)) return marker;
            if (notes.Contains(AppointmentNoteMarkers.FlowPrefix, StringComparison.OrdinalIgnoreCase)) return notes;
            return $"{notes} {marker}";
        }

        private static string AppendClusterMarker(string? notes, ServiceCluster cluster)
        {
            var marker = cluster == ServiceCluster.Heavy ? AppointmentNoteMarkers.ClusterHeavy : AppointmentNoteMarkers.ClusterQuick;
            if (string.IsNullOrWhiteSpace(notes)) return marker;
            if (notes.Contains(AppointmentNoteMarkers.ClusterPrefix, StringComparison.OrdinalIgnoreCase)) return notes;
            return $"{notes} {marker}";
        }

        private static string AppendNoteMarker(string? notes, string marker)
        {
            var tag = $"[{marker}]";
            if (string.IsNullOrWhiteSpace(notes)) return tag;
            if (notes.Contains(tag, StringComparison.OrdinalIgnoreCase)) return notes;
            return $"{notes} {tag}";
        }

        private static IEnumerable<long> ParseServiceIds(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return Enumerable.Empty<long>();
            return csv.Split(new[] { ',', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => long.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : 0)
                .Where(id => id > 0);
        }
    }
}
