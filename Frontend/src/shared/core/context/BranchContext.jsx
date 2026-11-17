import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import { request } from "@app/apiClient";
import { useAuth } from "@shared/core/context/AuthContext";

const BranchContext = createContext(undefined);
const STORAGE_KEY = "mygarage_branch_id";
const ALL_BRANCHES_ID = 0;

async function fetchBranches() {
  try {
    const response = await request({ url: "/api/v1/branches", method: "GET" });
    return response || [];
  } catch {
    return [];
  }
}

function parseBranchId(rawValue) {
  const raw = String(rawValue ?? "").trim().toLowerCase();
  if (!raw) return null;
  if (raw === "all") return ALL_BRANCHES_ID;
  const parsed = Number(raw);
  if (Number.isFinite(parsed) && parsed >= 0) return parsed;
  return null;
}

function isBranchInList(branchList, branchId) {
  if (branchId === ALL_BRANCHES_ID) return true;
  return branchList.some((item) => Number(item?.id) === Number(branchId));
}

function readStoredBranchId() {
  if (typeof window === "undefined") return null;
  try {
    return parseBranchId(window.localStorage.getItem(STORAGE_KEY));
  } catch {
    return null;
  }
}

function writeStoredBranchId(branchId) {
  if (typeof window === "undefined") return;
  try {
    window.localStorage.setItem(STORAGE_KEY, String(branchId));
  } catch {
    // Ignore storage write failures so the login page cannot crash.
  }
}

export function BranchProvider({ children }) {
  const { user } = useAuth();
  const [branches, setBranches] = useState([]);
  const [selectedBranchId, setSelectedBranchId] = useState(readStoredBranchId);
  const [loading, setLoading] = useState(false);

  const roleCode = String(user?.roleCode || "").trim().toUpperCase();
  const userBranchId = parseBranchId(user?.branchId);
  const isBranchLocked = (roleCode === "ACCOUNTANT" || roleCode === "RECEPTIONIST") && userBranchId !== null && userBranchId !== ALL_BRANCHES_ID;

  const refreshBranches = useCallback(async () => {
    setLoading(true);
    try {
      const list = await fetchBranches();
      const active = list.filter((item) => item?.isDeleted !== true);
      setBranches(active);
      return active;
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    let mounted = true;
    (async () => {
      try {
        const list = await refreshBranches();
        if (!mounted) return;

        setSelectedBranchId((currentId) => {
          if (!list.length) {
            writeStoredBranchId(ALL_BRANCHES_ID);
            return ALL_BRANCHES_ID;
          }

          if (isBranchLocked) {
            const lockedBranchId = isBranchInList(list, userBranchId) ? userBranchId : Number(list[0]?.id) || ALL_BRANCHES_ID;
            writeStoredBranchId(lockedBranchId);
            return lockedBranchId;
          }

          const nextId = isBranchInList(list, currentId) ? currentId : Number(list[0]?.id) || ALL_BRANCHES_ID;
          writeStoredBranchId(nextId);
          return nextId;
        });
      } catch {
      }
    })();

    return () => {
      mounted = false;
    };
  }, [refreshBranches, isBranchLocked, userBranchId]);

  useEffect(() => {
    if (!isBranchLocked) return;
    if (!branches.length) return;

    const lockedBranchId = isBranchInList(branches, userBranchId) ? userBranchId : Number(branches[0]?.id) || ALL_BRANCHES_ID;
    if (Number(selectedBranchId) === Number(lockedBranchId)) return;

    setSelectedBranchId(lockedBranchId);
    writeStoredBranchId(lockedBranchId);
  }, [isBranchLocked, userBranchId, branches, selectedBranchId]);

  const selectBranch = useCallback((branchId) => {
    if (isBranchLocked) {
      const lockedBranchId = isBranchInList(branches, userBranchId) ? userBranchId : Number(branches[0]?.id) || ALL_BRANCHES_ID;
      setSelectedBranchId(lockedBranchId);
      writeStoredBranchId(lockedBranchId);
      return;
    }

    const parsed = parseBranchId(branchId);
    if (parsed === null) return;
    setSelectedBranchId(parsed);
    writeStoredBranchId(parsed);
  }, [isBranchLocked, branches, userBranchId]);

  const selectedBranch = useMemo(
    () => branches.find((item) => Number(item?.id) === Number(selectedBranchId)) || null,
    [branches, selectedBranchId]
  );
  const isAllBranches = selectedBranchId === ALL_BRANCHES_ID;

  const value = useMemo(
    () => ({
      branches,
      selectedBranchId,
      selectedBranch,
      isAllBranches,
      isBranchLocked,
      selectBranch,
      refreshBranches,
      loading
    }),
    [branches, selectedBranchId, selectedBranch, isAllBranches, isBranchLocked, selectBranch, refreshBranches, loading]
  );

  return <BranchContext.Provider value={value}>{children}</BranchContext.Provider>;
}

export function useBranch() {
  const context = useContext(BranchContext);
  if (!context) {
    throw new Error("useBranch must be used within BranchProvider");
  }
  return context;
}
