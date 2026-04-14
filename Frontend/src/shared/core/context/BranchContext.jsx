import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import { request } from "@app/apiClient";
import { useAuth } from "@shared/core/context/AuthContext";

const BranchContext = createContext(undefined);
const STORAGE_KEY = "mygarage_branch_id";
const ALL_BRANCHES_ID = 0;

async function fetchBranches() {
  try {
    const response = await request({ url: "/api/branches", method: "GET" });
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
  const parsedUserBranchId = parseBranchId(user?.branchId);
  const lockedBranchId =
    roleCode === "ACCOUNTANT" &&
    Number.isFinite(Number(parsedUserBranchId)) &&
    Number(parsedUserBranchId) > 0
      ? Number(parsedUserBranchId)
      : null;
  const isBranchLocked = lockedBranchId !== null;

  const applyBranchScope = useCallback(
    (branchList) => {
      const activeList = (branchList || []).filter((item) => item?.isDeleted !== true);
      if (!isBranchLocked) return activeList;

      const scoped = activeList.filter((item) => Number(item?.id) === Number(lockedBranchId));
      if (scoped.length) return scoped;

      return [
        {
          id: Number(lockedBranchId),
          name: user?.branchName || `Chi nhánh #${lockedBranchId}`,
          isDeleted: false,
        },
      ];
    },
    [isBranchLocked, lockedBranchId, user?.branchName]
  );

  const refreshBranches = useCallback(async () => {
    setLoading(true);
    try {
      const list = await fetchBranches();
      const active = applyBranchScope(list);
      setBranches(active);
      return active;
    } finally {
      setLoading(false);
    }
  }, [applyBranchScope]);

  useEffect(() => {
    let mounted = true;
    (async () => {
      try {
        const list = await refreshBranches();
        if (!mounted) return;

        setSelectedBranchId((currentId) => {
          if (!list.length) {
            if (isBranchLocked && lockedBranchId) {
              writeStoredBranchId(lockedBranchId);
              return lockedBranchId;
            }
            writeStoredBranchId(ALL_BRANCHES_ID);
            return ALL_BRANCHES_ID;
          }

          if (isBranchLocked && lockedBranchId) {
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
  }, [refreshBranches, isBranchLocked, lockedBranchId]);

  const selectBranch = useCallback((branchId) => {
    let parsed = parseBranchId(branchId);
    if (parsed === null) return;
    if (isBranchLocked && lockedBranchId) {
      parsed = lockedBranchId;
    }
    setSelectedBranchId(parsed);
    writeStoredBranchId(parsed);
  }, [isBranchLocked, lockedBranchId]);

  const selectedBranch = useMemo(
    () => branches.find((item) => Number(item?.id) === Number(selectedBranchId)) || null,
    [branches, selectedBranchId]
  );
  const isAllBranches = !isBranchLocked && selectedBranchId === ALL_BRANCHES_ID;

  const value = useMemo(
    () => ({
      branches,
      selectedBranchId,
      selectedBranch,
      isAllBranches,
      isBranchLocked,
      lockedBranchId,
      selectBranch,
      refreshBranches,
      loading
    }),
    [
      branches,
      selectedBranchId,
      selectedBranch,
      isAllBranches,
      isBranchLocked,
      lockedBranchId,
      selectBranch,
      refreshBranches,
      loading,
    ]
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
