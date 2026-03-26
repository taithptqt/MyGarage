import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import { request } from "../app/apiClient";

const BranchContext = createContext(undefined);
const STORAGE_KEY = "mygarage_branch_id";

// Helper to get branches (avoiding dependency on admin feature api)
async function fetchBranches() {
  try {
    const response = await request({ url: "/api/Branch", method: "GET" });
    return response || [];
  } catch {
    return [];
  }
}

export function BranchProvider({ children }) {
  const [branches, setBranches] = useState([]);
  const [selectedBranchId, setSelectedBranchId] = useState(null);
  const [loading, setLoading] = useState(false);

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
    const saved = localStorage.getItem(STORAGE_KEY);
    if (!saved) return;
    const parsed = Number(saved);
    if (Number.isFinite(parsed) && parsed > 0) {
      setSelectedBranchId(parsed);
    }
  }, []);

  useEffect(() => {
    let mounted = true;
    (async () => {
      try {
        const list = await refreshBranches();
        if (!mounted) return;
        if (!list.length) {
          setSelectedBranchId(null);
          localStorage.removeItem(STORAGE_KEY);
          return;
        }

        const hasSaved = list.some((item) => item.id === selectedBranchId);
        const nextId = hasSaved ? selectedBranchId : list[0].id;
        setSelectedBranchId(nextId);
        localStorage.setItem(STORAGE_KEY, String(nextId));
      } catch {
        // Keep UI usable even when branch API temporarily fails.
      }
    })();

    return () => {
      mounted = false;
    };
  }, [refreshBranches]);

  const selectBranch = useCallback((branchId) => {
    const parsed = Number(branchId);
    if (!Number.isFinite(parsed) || parsed <= 0) return;
    setSelectedBranchId(parsed);
    localStorage.setItem(STORAGE_KEY, String(parsed));
  }, []);

  const selectedBranch = useMemo(
    () => branches.find((item) => item.id === selectedBranchId) || null,
    [branches, selectedBranchId]
  );

  const value = useMemo(
    () => ({
      branches,
      selectedBranchId,
      selectedBranch,
      selectBranch,
      refreshBranches,
      loading
    }),
    [branches, selectedBranchId, selectedBranch, selectBranch, refreshBranches, loading]
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
