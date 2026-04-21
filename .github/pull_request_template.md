## Summary

- [ ] Describe key business changes
- [ ] List impacted backend files/modules

## Transaction Policy Checklist

- [ ] I reviewed `backend/Docs/transaction-policy-matrix.md`
- [ ] For every changed `MustTx` file, I used the standard pattern:
  `TryBeginTransactionAsync` + `CurrentTransaction` guard + execution strategy
- [ ] I did not add direct `Database.BeginTransactionAsync(...)` allocation in mutation paths
- [ ] I validated that `SaveChangesAsync` in mutation flows is wrapped by transaction helpers/policy
- [ ] I ran `pwsh ./scripts/check-transaction-policy.ps1` locally

## Validation

- [ ] `dotnet build backend/MyGarage.csproj` passes
- [ ] Relevant tests/manual checks were run
