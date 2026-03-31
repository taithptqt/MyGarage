import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import { getAllBranches } from "../features/admin/api/branchApi";
import { useAuth } from "./AuthContext";
import { isAccountingStaff } from "../app/staffHome";

const BranchContext = createContext(undefined);
const STORAGE_KEY = "mygarage_branch_id";

function removeDiacritics(value) {
  return String(value || "")
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/\u0111/g, "d")
    .replace(/\u0110/g, "D");
}

function normalizeBranchName(value) {
  const normalized = removeDiacritics(value)
    .toLowerCase()
    .replace(/[^a-z0-9\s]/g, " ")
    .replace(/\s+/g, " ")
    .trim();

  if (!normalized) return "";

  if (["hcm", "tp hcm", "tphcm", "ho chi minh", "sai gon", "saigon"].includes(normalized)) {
    return "ho chi minh";
  }
  if (["ha noi", "hanoi"].includes(normalized)) {
    return "ha noi";
  }
  if (["da nang", "danang"].includes(normalized)) {
    return "da nang";
  }

  return normalized;
}

function scoreBranch(branch) {
  let score = 0;
  const name = String(branch?.name || "");
  if (removeDiacritics(name) !== name) score += 2;
  if (String(branch?.status || "").toLowerCase() === "active") score += 1;
  if (branch?.code) score += 1;
  return score;
}

function pickPreferredBranch(current, candidate) {
  if (!current) return candidate;

  const currentScore = scoreBranch(current);
  const candidateScore = scoreBranch(candidate);
  if (candidateScore > currentScore) return candidate;
  if (candidateScore < currentScore) return current;

  const currentId = Number(current?.id);
  const candidateId = Number(candidate?.id);
  if (Number.isFinite(currentId) && Number.isFinite(candidateId)) {
    return candidateId < currentId ? candidate : current;
  }

  return current;
}

function dedupeBranches(list) {
  const deduped = new Map();

  for (const branch of list || []) {
    if (!branch || branch.isDeleted === true) continue;

    const normalizedName = normalizeBranchName(branch.name);
    const key = normalizedName || `id:${branch.id}`;
    const existing = deduped.get(key);
    deduped.set(key, pickPreferredBranch(existing, branch));
  }

  return Array.from(deduped.values());
}

export function BranchProvider({ children }) {
  const { user } = useAuth();
  const [branches, setBranches] = useState([]);
  const [selectedBranchId, setSelectedBranchId] = useState(null);
  const [loading, setLoading] = useState(false);
  const accountingBranchId = useMemo(() => {
    const parsed = Number(user?.branchId);
    if (!isAccountingStaff(user)) return null;
    if (!Number.isFinite(parsed) || parsed <= 0) return null;
    return parsed;
  }, [user]);
  const isBranchLocked = accountingBranchId !== null;

  const refreshBranches = useCallback(async () => {
    setLoading(true);
    try {
      const list = (await getAllBranches()) || [];
      const active = list.filter((item) => item?.isDeleted !== true);
      const scoped = accountingBranchId
        ? active.filter((item) => Number(item?.id) === accountingBranchId)
        : dedupeBranches(active);
      setBranches(scoped);
      return scoped;
    } finally {
      setLoading(false);
    }
  }, [accountingBranchId]);

  useEffect(() => {
    if (accountingBranchId) {
      setSelectedBranchId(accountingBranchId);
      localStorage.setItem(STORAGE_KEY, String(accountingBranchId));
      return;
    }

    const saved = localStorage.getItem(STORAGE_KEY);
    if (!saved) return;
    const parsed = Number(saved);
    if (Number.isFinite(parsed) && parsed > 0) {
      setSelectedBranchId(parsed);
    }
  }, [accountingBranchId]);

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

        if (accountingBranchId) {
          const hasAccountantBranch = list.some((item) => Number(item?.id) === accountingBranchId);
          const nextId = hasAccountantBranch ? accountingBranchId : null;
          setSelectedBranchId(nextId);
          if (nextId) {
            localStorage.setItem(STORAGE_KEY, String(nextId));
          } else {
            localStorage.removeItem(STORAGE_KEY);
          }
          return;
        }

        const saved = Number(localStorage.getItem(STORAGE_KEY));
        const savedId = Number.isFinite(saved) && saved > 0 ? saved : null;
        const hasSaved = savedId ? list.some((item) => item.id === savedId) : false;
        const nextId = hasSaved ? savedId : list[0].id;
        setSelectedBranchId(nextId);
        localStorage.setItem(STORAGE_KEY, String(nextId));
      } catch {
        // Keep UI usable even when branch API temporarily fails.
      }
    })();

    return () => {
      mounted = false;
    };
  }, [refreshBranches, accountingBranchId]);

  const selectBranch = useCallback((branchId) => {
    const parsed = Number(branchId);
    if (!Number.isFinite(parsed) || parsed <= 0) return;
    if (accountingBranchId && parsed !== accountingBranchId) return;
    setSelectedBranchId(parsed);
    localStorage.setItem(STORAGE_KEY, String(parsed));
  }, [accountingBranchId]);

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
      loading,
      isBranchLocked
    }),
    [branches, selectedBranchId, selectedBranch, selectBranch, refreshBranches, loading, isBranchLocked]
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
