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

            int quickBooked = overlap.Count(a => ResolveFlowByRequestedServices(a.RequestedServiceIds, a.RequestedPackageIds, a.CustomerNotes) == ServiceCluster.Quick);
            int heavyBooked = overlap.Count - quickBooked;
            int totalBooked = overlap.Count;
            int remainingByOnlineCap = Math.Max(0, onlineLimit - totalBooked);
            var clusterCaps = ComputeClusterOnlineCaps(capacity, onlineLimit);

            int heavyMax = clusterCaps.repairCap;
            int heavyRemaining = Math.Min(Math.Max(0, heavyMax - heavyBooked), remainingByOnlineCap);

            int repairFree = Math.Max(0, clusterCaps.repairCap - heavyBooked);
            double repairFreeRatio = clusterCaps.repairCap > 0
                ? repairFree / (double)clusterCaps.repairCap
                : 0d;
            bool quickFullyBooked = quickBooked >= clusterCaps.quickCap;
            bool allowQuickOverflow = quickFullyBooked && repairFreeRatio > 0.5d;
            int quickOverflow = allowQuickOverflow ? repairFree : 0;
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
            var quickCap = Math.Max(0, capacity.Quick);
            var repairCap = Math.Max(0, capacity.Repair);

            // Keep cluster caps bounded by the effective online limit.
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
