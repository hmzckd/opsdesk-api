# Changelog

This file records notable OpsDesk API releases.

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
