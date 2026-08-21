# V2 Ticket Lifecycle Contract

Status: Approved by V2-D03

## Purpose

This document defines valid Ticket status transitions, the Users allowed to perform them, and the status-change history created by successful transitions.

Ticket attributes are defined in the [V2 Ticket Domain Contract](./v2-ticket-domain.md). Ticket visibility is defined in the [V2 Ticket Access Contract](./v2-ticket-access.md).

## Lifecycle Principles

- Every Ticket starts as `open`.
- Status is server-controlled and can change only through the approved lifecycle endpoint.
- Customers do not manage operational work states.
- The requester can accept a resolution by closing the Ticket or reject it by reopening the Ticket.
- Agents and Admins can manage operational transitions on every visible Ticket.
- Assignment is not a prerequisite in V2 because assignment behavior belongs to V2.1.
- `closed` is terminal in V2.
- A successful transition updates the Ticket and records one Ticket Status Change atomically.

## Transition Matrix

| Previous status | New status | Customer requester | Agent | Admin | Meaning |
|---|---|---:|---:|---:|---|
| `open` | `in_progress` | No | Yes | Yes | Support begins active work. |
| `in_progress` | `waiting_customer` | No | Yes | Yes | Support requires requester input. |
| `waiting_customer` | `in_progress` | No | Yes | Yes | Support resumes active work. |
| `in_progress` | `resolved` | No | Yes | Yes | Support provides a resolution. |
| `waiting_customer` | `resolved` | No | Yes | Yes | Support resolves after receiving enough information. |
| `resolved` | `in_progress` | Yes | Yes | Yes | The resolution is rejected or more work is required. |
| `resolved` | `closed` | Yes | Yes | Yes | The resolution is accepted and work ends. |

Every transition not listed in the matrix is invalid. Requester permissions apply only to the Customer who owns the Ticket. Other Customers cannot discover the Ticket and receive `404 Not Found` under the visibility contract.

## Timestamp Rules

- Every successful transition updates `UpdatedAtUtc`.
- Entering `resolved` sets `ResolvedAtUtc` to the transition time.
- Leaving `resolved` clears `ResolvedAtUtc`.
- Entering `closed` sets `ClosedAtUtc` to the transition time.
- `closed` cannot be left in V2.
- Failed, forbidden, and no-op requests do not modify any timestamp.

## Status Change Record

Each successful transition records exactly one user-visible Ticket Status Change containing:

- `Id`
- `TicketId`
- `ActorId`
- `PreviousStatus`
- `NewStatus`
- `CreatedAtUtc`

The Ticket update and its status-change record are committed in one database transaction. Either both persist or neither persists.

Ticket Status History is separate from the internal admin Audit Log. It contains only user-visible lifecycle changes and does not contain authentication data, passwords, tokens, or arbitrary request payloads.

## Public API Contract

The approved transition endpoint is:

```http
PATCH /tickets/{id}/status
Content-Type: application/json

{
  "status": "in_progress"
}
```

A successful transition returns `200 OK` with the updated `TicketResponse`.

## HTTP Outcome Rules

| Situation | HTTP outcome |
|---|---:|
| Missing or invalid authentication | `401 Unauthorized` |
| Ticket does not exist or is outside Customer visibility | `404 Not Found` |
| Ticket is visible but the role cannot perform the requested transition | `403 Forbidden` |
| Status value cannot be parsed | `400 Bad Request` |
| Status is valid but the transition is not in the matrix, including a no-op | `409 Conflict` |
| Transition succeeds | `200 OK` |

Authorization is evaluated before transition validity for a visible Ticket. A Customer cannot probe lifecycle details for another requester's Ticket.

## Representative Gherkin Scenarios

```gherkin
Scenario: Agent starts work on an open Ticket
  Given an open Ticket requested by a Customer
  And an authenticated Agent
  When the Agent changes the Ticket status to in_progress
  Then the API returns 200 OK
  And the Ticket status becomes in_progress
  And one status-change record is persisted

Scenario: Customer attempts an operational transition
  Given the Customer's open Ticket
  When the Customer changes the Ticket status to in_progress
  Then the API returns 403 Forbidden
  And neither the Ticket nor status history changes

Scenario: Agent attempts an invalid transition
  Given an open Ticket
  And an authenticated Agent
  When the Agent changes the Ticket status to resolved
  Then the API returns 409 Conflict
  And neither the Ticket nor status history changes

Scenario: Requester rejects a resolution
  Given the Customer's resolved Ticket
  When the Customer changes the Ticket status to in_progress
  Then the API returns 200 OK
  And the resolved timestamp is cleared
  And one status-change record is persisted
```

## Test Contract

- Domain tests cover every allowed transition and representative invalid/no-op transitions.
- API tests observe `200`, `400`, `401`, `403`, `404`, and `409` outcomes through the public endpoint.
- PostgreSQL integration tests verify the Ticket update and status-change record persist together.
- Forbidden and invalid operations are verified through public responses and unchanged observable state, not internal call counts.

## Out of Scope

- Automatic transitions triggered by comments
- Assignment requirements or assignment history
- SLA timer behavior
- Background auto-close rules
- Internal admin audit logs
- Reopening a closed Ticket
