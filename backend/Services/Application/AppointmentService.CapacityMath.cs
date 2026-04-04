using System;
using System.Collections.Generic;
using System.Linq;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        private static SlotEvaluation EvaluateSlot(List<Appointment> overlap, ServiceCluster requestedCluster, CapacityProfile capacity, int onlineLimit)
        {
            if (onlineLimit <= 0 || capacity.Total <= 0)
            {
                return new SlotEvaluation(false, 0, 0, 0, 0);
            }

            int quickBooked = overlap.Count(a => ResolveFlowByRequestedServices(a.RequestedServiceIds, a.CustomerNotes) == ServiceCluster.Quick);
            int heavyBooked = overlap.Count - quickBooked;
            int totalBooked = overlap.Count;
            int remainingByOnlineCap = Math.Max(0, onlineLimit - totalBooked);
            var clusterCaps = ComputeClusterOnlineCaps(capacity, onlineLimit);

            int heavyMax = clusterCaps.repairCap;
            int heavyRemaining = Math.Min(Math.Max(0, heavyMax - heavyBooked), remainingByOnlineCap);

            int quickOverflow = Math.Max(0, clusterCaps.repairCap - heavyBooked - ReservedRepairForQuickOverflow);
            int quickMax = clusterCaps.quickCap + quickOverflow;
            int quickRemaining = Math.Min(Math.Max(0, quickMax - quickBooked), remainingByOnlineCap);

            if (requestedCluster == ServiceCluster.Heavy)
            {
                return new SlotEvaluation(heavyRemaining > 0, heavyRemaining, heavyMax, quickBooked, heavyBooked);
            }

            return new SlotEvaluation(quickRemaining > 0, quickRemaining, quickMax, quickBooked, heavyBooked);
        }

        private static (int quickCap, int repairCap) ComputeClusterOnlineCaps(CapacityProfile capacity, int onlineLimit)
        {
            var quickCap = capacity.Quick <= 0
                ? 0
                : (int)Math.Round(capacity.Quick * 0.85m, MidpointRounding.AwayFromZero);
            var repairCap = capacity.Repair <= 0
                ? 0
                : (int)Math.Round(capacity.Repair * 0.85m, MidpointRounding.AwayFromZero);

            // Defensive guard: keep cluster caps not exceeding overall online limit.
            if (quickCap + repairCap > onlineLimit)
            {
                var excess = (quickCap + repairCap) - onlineLimit;
                if (repairCap >= excess)
                {
                    repairCap -= excess;
                }
                else
                {
                    var remain = excess - repairCap;
                    repairCap = 0;
                    quickCap = Math.Max(0, quickCap - remain);
                }
            }

            return (quickCap, repairCap);
        }
    }
}
