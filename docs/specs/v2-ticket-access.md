# V2 Ticket Access Contract

Status: Approved by V2-D02

## Purpose

This document defines who can create, discover, and view Tickets in V2. It also defines the public HTTP outcomes used to prevent protected Ticket information from leaking.

The Ticket attributes themselves are defined in the [V2 Ticket Domain Contract](./v2-ticket-domain.md).

## Access Principles

- Every Ticket endpoint requires authentication.
- Every authenticated User can create a Ticket for themselves.
- The authenticated creator always becomes the Requester.
- V2 does not allow creating a Ticket on behalf of another User.
- Customers can discover and view only Tickets where they are the Requester.
- Agents and Admins can discover and view every Ticket so that unassigned work can be found and handled.
- Assignment does not restrict Agent or Admin visibility.
- Visibility rules apply consistently to Ticket details and later Ticket comments, status history, and activity views.

## Role Matrix

| Actor | Create for self | Create for another User | Discover Tickets | View Ticket details |
|---|---:|---:|---|---|
| Anonymous caller | No | No | None | None |
| Customer | Yes | No | Own Tickets only | Own Tickets only |
| Agent | Yes | No | All Tickets | All Tickets |
| Admin | Yes | No | All Tickets | All Tickets |

## Creation Contract

`POST /tickets` requires an authenticated User but does not require an elevated role.

The API derives `RequesterId` from the authenticated identity. A request cannot provide or override `RequesterId`.

A successful creation returns `201 Created` and identifies the newly created Ticket resource. Input validation failures return `400 Bad Request` without creating a Ticket.

## Detail Visibility Contract

`GET /tickets/{id}` applies both authentication and Ticket visibility.

An authorized viewer receives the Ticket-owned fields approved in V2-D01. V2 does not expose password data, token data, or additional User profile information through the Ticket response.

Requester profile summaries can be introduced only through a separate approved contract.

## HTTP Outcome Rules

| Situation | HTTP outcome | Reason |
|---|---:|---|
| Missing or invalid authentication | `401 Unauthorized` | The caller has no valid identity. |
| Authenticated caller requests a Ticket that does not exist | `404 Not Found` | No resource can be returned. |
| Customer requests another requester's Ticket | `404 Not Found` | The API must not reveal whether a protected Ticket exists. |
| Authenticated caller can view the Ticket | `200 OK` | The Ticket is visible to that identity. |
| Authenticated caller submits invalid creation input | `400 Bad Request` | The request contract is invalid. |

`403 Forbidden` is reserved for actions on a visible Ticket that the authenticated caller is not permitted to perform, such as a future restricted lifecycle transition. It is not used to reveal the existence of a Ticket outside the caller's visibility scope.

## Enforcement Boundary

- API authentication establishes the caller's identity.
- Application use cases apply role and requester visibility rules.
- Infrastructure queries must support visibility-aware reads and lists.
- Controllers must not reconstruct Ticket visibility rules independently.
- Collection queries introduced in V22-D01 must apply the same visibility scope before pagination.

## Test Contract

V2 implementation tasks must cover:

- Authenticated creation with the requester derived from the token
- Anonymous creation returning `401 Unauthorized`
- Customer access to an own Ticket
- Customer access to another requester's Ticket returning `404 Not Found`
- Agent access to any Ticket
- Admin access to any Ticket
- Unknown Ticket access returning `404 Not Found`

Tests observe HTTP behavior and returned data. They do not assert controller method calls, repository call counts, or other implementation details.

## Out of Scope

- Creating Tickets on behalf of another User
- Team-based visibility
- Internal-only comments or notes
- Field-level redaction between authorized roles
- Assignment permissions
- Status transition permissions
- Admin audit-log access
