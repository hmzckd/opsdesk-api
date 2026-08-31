# Changelog

This file records notable OpsDesk API releases.

## 2.1.0 - 2026-08-31

### Added

- Admin-only Agent account provisioning with server-owned roles and validated credentials.
- Public Ticket comments with server-owned authorship and role-aware visibility.
- Agent self-assignment and Admin-managed assignment and unassignment flows.
- Assignment activity records with optimistic concurrency protection.
- A unified Ticket activity timeline for comments, status changes, and assignments.
- Requester-confirmed closure after support resolution.
- A dedicated requester-only reopen flow with a required public reason.

### Changed

- Agent visibility and mutation rules now respect Ticket ownership boundaries.
- Activity responses expose safe actor summaries and hide assignment history from Customers.
- Resolved Tickets can only be reopened through the dedicated atomic reopen workflow.

### Validation

- Release build completed with zero warnings and zero errors.
- All 154 unit and PostgreSQL integration tests passed with no skipped tests.
- Line coverage: 96.57% (3,240 of 3,355 lines).
- Branch coverage: 80.18% (263 of 328 branches).
- EF Core reported no model changes pending a migration.
- Repository diff and tracked-secret checks completed without release blockers.
- Manual Swagger acceptance covered Agent provisioning, login, and Ticket assignment.

## 2.0.0 - 2026-08-24

### Added

- Authenticated Ticket creation with server-owned defaults and requester identity.
- Role-aware Ticket visibility without exposing another Customer's Tickets.
- Ticket lifecycle transitions for Customers, Agents, and Admins.
- Atomic persistence of Ticket status and status-history records.
- Chronological status-history endpoint with actor IDs and transition times.
- PostgreSQL Testcontainers coverage for migrations, constraints, and transactions.

### Changed

- OpenAPI now publishes required Ticket text fields and their maximum lengths.
- README endpoint and roadmap documentation now reflects the v2 release structure.

### Validation

- Release build completed with zero warnings and zero errors.
- All 73 unit and integration tests passed against PostgreSQL.
- Line coverage: 95.35% (1,663 of 1,744 lines).
- Branch coverage: 76.19% (128 of 168 branches).
- EF Core reported no model changes pending a migration.
- Core Swagger Ticket flow was manually accepted before release preparation.
