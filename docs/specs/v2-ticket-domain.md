# V2 Ticket Domain Contract

Status: Approved by V2-D01

## Purpose

This document defines the data owned by a Ticket before persistence, API endpoints, and lifecycle behavior are implemented.

Authorization and visibility rules are defined in the [V2 Ticket Access Contract](./v2-ticket-access.md). Valid status transitions are defined separately in V2-D03.

## Ticket Attributes

| Attribute | Ownership | Required | Nullable | Rule |
|---|---|---:|---:|---|
| `Id` | Server | Yes | No | Generated as a Guid when the Ticket is created. |
| `Title` | Creator | Yes | No | Trimmed, non-blank, maximum 200 characters. |
| `Description` | Creator | Yes | No | Trimmed, non-blank, maximum 5000 characters. |
| `Priority` | Creator with server default | No | No | Defaults to Medium when omitted. |
| `Status` | Server | Yes | No | Always starts as Open. |
| `RequesterId` | Server | Yes | No | Taken from the authenticated identity. |
| `AssigneeId` | Server | No | Yes | Starts as null and can later reference an Agent. |
| `CreatedAtUtc` | Server | Yes | No | Set to the current UTC time at creation. |
| `UpdatedAtUtc` | Server | Yes | No | Initially equals CreatedAtUtc and changes when Ticket-owned state changes. |
| `ResolvedAtUtc` | Server | No | Yes | Set by the lifecycle workflow when the Ticket becomes Resolved. |
| `ClosedAtUtc` | Server | No | Yes | Set by the lifecycle workflow when the Ticket becomes Closed. |

## Creation Input

A Ticket creation request can provide only:

- `title`
- `description`
- `priority`

The creator cannot provide or override identifiers, status, assignment, requester identity, or timestamps.

## Status Values

The V2 Ticket status values are:

- `open`
- `in_progress`
- `waiting_customer`
- `resolved`
- `closed`

The initial status is always `open`.

This task defines the vocabulary only. Allowed transitions and role permissions are deferred to V2-D03.

## Priority Values

The V2 Ticket priority values are:

- `low`
- `medium`
- `high`
- `urgent`

Priority is optional in the creation request and defaults to `medium`.

Support-driven reprioritization and AI priority suggestions are outside V2-D01.

## Text Rules

- Title and description are trimmed before validation and persistence.
- Whitespace-only values are invalid.
- Unicode text is accepted.
- Description line breaks are preserved.
- HTML rendering and sanitization are outside V2-D01 because the current product is an API.

## Identifier Decision

V2 uses the Guid `Id` as the Ticket identifier in storage and API routes.

A separate human-readable Ticket number is not included in V2. It can be introduced later if operational workflows demonstrate a real need.

## Timestamp Rules

- All timestamps are stored and returned in UTC.
- Clients cannot submit authoritative timestamps.
- `UpdatedAtUtc` represents changes to Ticket-owned state.
- Comment timestamps belong to comments and are not Ticket attributes.

## Deferred Decisions

The following decisions belong to later tasks:

- Valid status transitions and actors: V2-D03
- Comment rules: V21-D01
- Assignment behavior: V21-D01
- Pagination, filtering, and sorting: V22-D01
- SLA, internal audit logs, approvals, and background jobs: V3
- AI-generated priority recommendations: V4

## Decision Summary

- Canonical field name is Title, not Subject.
- Requester means the User represented by RequesterId.
- Assignee means the optional Agent represented by AssigneeId.
- Status is server-controlled.
- Priority is creator-selectable and defaults to Medium.
- V2 uses Guid identifiers without a separate Ticket number.
