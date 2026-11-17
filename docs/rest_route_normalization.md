# REST routes: canonical paths and compatibility

This document records the route normalization implemented in the current workspace. Existing aliases remain supported; clients outside this repository have not been inventoried. A canonical path identifies the same action and contract as its listed legacy path. This document does not assert that every endpoint in the system has been reviewed.

| Scope | Canonical path | Existing path kept |
| --- | --- | --- |
| Staff | `/api/v1/staff` and `/api/v1/staff/{id}` | `/api/v1/staffs` and `/api/v1/staffs/{id}` |
| Bay administration | `/api/v1/admin/bays` | `/api/v1/bay-admin/bays` and existing administrative aliases |
| Vehicle administration | `/api/v1/admin/vehicles` | `/api/v1/vehicle-admin/vehicles` and existing administrative aliases |
| Diagnosis under service order | `/api/v1/service-orders/{id}/…` | `/api/v1/diagnoses/service-orders/{id}/…` for the corresponding diagnosis actions |
| Campaign vouchers | `/api/v1/voucher-campaigns/{id}/vouchers` | `/api/v1/voucher-campaigns/{id}/voucher-page` |
| Purchase order receipts | `/api/v1/purchase-orders/{id}/goods-receipts` | `/api/v1/purchase-orders/{id}/receive-history` |
| Product gallery upload | `/api/v1/admin/products/{id}/images` | `/api/v1/admin/products/{id}/gallery-image` and `/api/v1/admin/parts/{id}/gallery-image` |
| Salary profiles | `/api/v1/salary-profiles` | `/api/v1/salary-admin/profiles` |
| Initial salary requests | `/api/v1/initial-salary-requests` | `/api/v1/salary-admin/requests` |
| Current customer appointments | `/api/v1/customers/current/appointments` | `/api/v1/appointments/customers/current` |
| Branch appointments | `/api/v1/branches/{branchId}/appointments` | `/api/v1/appointments/branches/{branchId}` |
| Current customer retail orders | `/api/v1/customers/current/retail-orders` and `/page` | `/api/v1/retail-orders/customers/current` and `/page` |
| Current staff payslips | `/api/v1/staff/current/payslips` | `/api/v1/payslips/my` |
| Customer conversations | `/api/v1/chat/conversations` | `/api/v1/chat/my-conversations` for GET |
| Service order reminder schedule | `/api/v1/service-orders/{id}/maintenance-reminder-schedules` | `/api/v1/maintenance-reminders/service-orders/{id}/schedules` |

Paths shown without a method are shorthand for the corresponding actions explicitly present in the controllers; they do not imply every HTTP method exists. Projection endpoints with different response contracts are not assumed to be interchangeable. OData routes are outside this REST route migration. No legacy endpoint is scheduled for removal without an external-client inventory and a compatibility plan.