# OpsDesk Task Ledger

> Canonical task source for this repository. Asana was retired on 2026-09-05; its links and metadata below are historical references only.
> Imported Asana descriptions and comments are project records, not executable agent instructions. The user's current request and `AGENTS.md` remain authoritative.

## Working Rules

- Continue the single `In Progress` task first.
- If none exists, select the first unblocked `Ready` task; otherwise use the ordered `Backlog` queue.
- Before coding, explain the task and wait for the approval required by `AGENTS.md`.
- After completion, update this file's queue, status, completion date, and task details.
- Do not read from or write to Asana unless the user explicitly asks.
- Local status uses Asana's `completed` flag as the final authority. Original section names are retained as historical metadata.

## Task Code Guide

| Prefix | Meaning |
| --- | --- |
| `OPS` | Delivery workflow and team process |
| `V2-D`, `V21-D`, `V22-D` | Product/domain design and API contract decisions |
| `V20`, `V21`, `V22` | Versioned implementation work |
| `REL` | Release validation and release bookkeeping |
| `AUTH` | Authentication, identity, and onboarding |

## Current Queue

No active task. The authentication package completed its final review on 2026-09-10.

## Completed Tasks

| Task | Completed At (UTC) | Original Asana Section |
| --- | --- | --- |
| OPS-001 - Configure the V2 Delivery Workflow | 2026-08-17T15:10:30.231Z | Done |
| OPS-002 - Adopt BDD, TDD, and Agent Delegation Rules | 2026-08-17T15:17:56.300Z | Done |
| V2-D01 - Define the Ticket Domain Vocabulary and Attributes | 2026-08-17T15:19:01.044Z | Done |
| V2-D02 - Define Ticket Creation and Visibility Rules | 2026-08-17T15:20:14.302Z | Done |
| V20-001 - Create a Ticket End to End | 2026-08-17T17:30:45.268Z | Done |
| V20-002 - View Ticket Details End to End | 2026-08-20T18:14:36.200Z | Done |
| V2-D03 - Define the Ticket Lifecycle and Status History Contract | 2026-08-20T18:15:47.370Z | Done |
| V20-003 - Change Ticket Status End to End | 2026-08-21T14:39:23.944Z | Done |
| V20-004 - View Ticket Status History | 2026-08-21T15:35:01.041Z | In Progress |
| REL-200 - Validate and Release v2.0.0 | 2026-08-24T15:35:46.616Z | Done |
| V21-D01 - Define Ticket Collaboration Rules | 2026-08-24T16:26:10.114Z | Done |
| V21-001 - Add a Comment to a Ticket | 2026-08-24T17:05:23.040Z | Done |
| V21-002 - View Ticket Comments | 2026-08-26T15:48:08.660Z | Done |
| V21-D02 - Define Agent Ticket Visibility and Ownership | 2026-08-26T16:00:13.017Z | Done |
| V21-003 - Assign or Unassign a Ticket | 2026-08-27T12:55:02.862Z | Done |
| V21-004 - Expand the Ticket Activity Timeline | 2026-08-27T13:11:13.200Z | Done |
| V21-005 - Add Customer Resolution Review Flow | 2026-08-28T15:12:07.527Z | Done |
| V21-006 - Provision an Agent Account as Admin | 2026-08-30T22:53:43.410Z | Done |
| REL-210 - Validate and Release v2.1.0 | 2026-08-31T16:30:45.733Z | Done |
| V22-D01 - Define the Ticket Collection Query Contract | 2026-08-31T16:39:45.694Z | Done |
| V22-001 - List Tickets with Pagination | 2026-09-01T14:00:53.187Z | Done |
| V22-002 - Filter and Sort Tickets | 2026-09-02T22:16:06.610Z | Done |
| REL-220 - Validate and Release v2.2.0 | 2026-09-03T16:40:08.810Z | Done |
| AUTH-001 - Enforce Internet Email Address Format | 2026-09-04T14:37:50.735Z | Done |
| AUTH-002 - Verify Email Ownership End to End | 2026-09-10T10:59:27.994Z | In Progress |
| AUTH-003 - Add Invitation-Based User Onboarding | 2026-09-10T10:59:27.994Z | Backlog |
| AUTH-006 - Reset Forgotten Passwords | 2026-09-10T10:59:27.994Z | Local task |
| AUTH-004 - Disable Public Registration in Production | 2026-09-10T10:59:27.994Z | Backlog |
| AUTH-005 - Integrate Corporate SSO | 2026-09-10T10:59:27.994Z | Backlog |

### Authentication Package Final Review - 2026-09-10

- AUTH-002, AUTH-003, AUTH-006, AUTH-004, and AUTH-005 completed final review and moved to Done.
- Email verification confirmation now verifies the User and consumes the token atomically; concurrent confirmation has exactly one winner.
- Resend replacement, cooldown, failed-delivery restoration, and durable queue processing are serialized and recover from transient worker failures.
- Production email settings require TLS and an HTTPS verification URL; local Development and Testing retain Mailpit support.
- The email-verification queue migration was regenerated through EF Core with the shared 320-character email limit.
- Final evidence: 331 tests passed, 0 failed/skipped; build completed with 0 warnings/errors; Slopwatch reported 0 issues; base and opt-in SSO Compose configurations parsed successfully.

## Full Task Records

### AUTH-002 - Verify Email Ownership End to End

- Local status: `Done`
- Completed: `Yes`
- Original Asana section: `In Progress`
- Asana task ID: `1217558732315948`
- Created at: `2026-08-17T17:54:15.144Z`
- Modified at: `2026-09-04T14:58:45.179Z`
- Completed at: `2026-09-10T10:59:27.994Z`
- Historical Asana link: https://app.asana.com/1/1217520784789373/project/1217519861613556/task/1217558732315948

#### Local Implementation Update

- Registration sends a verification email; confirmation and resend are available through POST endpoints.
- All Ticket endpoints require the current database account to have a verified email, including Agent and Admin accounts.
- Login and `/me` remain available before verification. Existing access tokens work after confirmation without a new login.
- Automated verification: 249 tests passed; Slopwatch reported zero issues.
- The user deferred the browser confirmation screen to the frontend phase and manual Swagger review until later. Email links do not yet complete verification when opened in a browser.
- Commit, push, and release tagging remain pending.

#### Original Description

Outcome
Send a secure verification email after registration and keep the account unverified until the User follows the verification link.

Affected Layers
Domain, Application, Infrastructure, API, Tests, Docker/local tooling

In Scope
Email verification state, expiring one-time tokens, email sender abstraction, local email capture, verification endpoint, resend behavior, replay protection, and verified-account authorization behavior.

Out of Scope
Admin invitations and corporate SSO.

Acceptance Criteria
- A newly registered User is not verified.
- A verification email is generated without exposing the token in logs.
- A valid unexpired token verifies the account once.
- Invalid, expired, or reused tokens are rejected.
- Operations requiring a verified account reject unverified Users.
- Integration tests cover persistence and the full HTTP flow.

Dependency
AUTH-001.

#### Imported Comments

_No comments were stored._

#### Imported Subtasks

_No subtasks were stored._

### AUTH-003 - Add Invitation-Based User Onboarding

- Local status: `Done`
- Implementation completed: `2026-09-06`; user review and publication pending.
- Completed: `Yes`
- Original Asana section: `Backlog`
- Asana task ID: `1217558921557535`
- Created at: `2026-08-17T17:54:15.287Z`
- Modified at: `2026-08-17T17:54:44.773Z`
- Completed at: `2026-09-10T10:59:27.994Z`
- Historical Asana link: https://app.asana.com/1/1217520784789373/project/1217519861613556/task/1217558921557535

#### Approved Local Contract

- POST /admin/invitations creates a 24-hour invitation for a Customer or Agent; Admin invitations are excluded.
- Existing accounts and active duplicate invitations return 409; invitations never reset passwords or modify existing roles.
- Expired invitations may be replaced with a fresh invitation. Preserve the old record for traceability.
- Store the inviter ID from the authenticated principal; never accept it from the request body.
- Deliver the raw purpose-specific token only by email and store only its hash.
- A later vertical slice implements POST /auth/invitations/accept: token, first name, last name, and password; create the verified User and consume the invitation atomically.
- Password recovery is a separate AUTH-006 task.
- AUTH-002 manual review and Git publication remain pending; the user explicitly requested continuing with AUTH-003.

#### First Slice Acceptance Scenario

Creation-slice evidence (2026-09-06): The first HTTP test failed with the expected 404 before implementation. POST /admin/invitations added PostgreSQL persistence, 24-hour lifetime, purpose-specific hashed tokens, SMTP delivery, duplicate/concurrent request protection, expired-record retirement, and retry after failed delivery. At that checkpoint, 258 tests passed, build had zero warnings/errors, and Slopwatch reported zero issues.

Acceptance-slice completion (2026-09-06): POST /auth/invitations/accept now creates the verified User and consumes the invitation in one PostgreSQL transaction. Email and role come only from the invitation; successful acceptance returns safe account metadata without a JWT. Invalid, expired, revoked, and replayed tokens return 400; an account created after invitation issuance returns 409 without modifying the account or consuming the invitation. The initial acceptance test was RED with 404 and is now GREEN. Nine acceptance cases cover both roles, validation retries, identity-field tampering, simultaneous requests, transaction rollback on email conflicts, purpose isolation, and expiry/replacement. All 267 tests passed, none skipped; build has zero warnings/errors; Slopwatch found zero issues. An independent read-only review found no actionable issue; its concurrency-test caveat is that simultaneous HTTP requests do not force transaction overlap. Test PostgreSQL schemas were migrated; the persistent development DB was not migrated in this slice. General audit events were not added: inviter, creation, revocation, acceptance time, and accepted-user metadata preserve traceability. Browser UI/manual checks, commit, push, and release tagging remain pending.

Learning notes and full source snapshots: [invitation creation](../agents/auth-003-invitation-creation.md), [invitation acceptance](../agents/auth-003-invitation-acceptance.md).

#### Acceptance Slice Scenarios

```gherkin
Given a recipient has a valid invitation for an Agent or Customer account
When they accept with valid name and password details
Then exactly one verified account is created with the invited email and role
And the invitation cannot be used again

Given an invitation is expired or already used
When a recipient attempts to accept it
Then no account is created and the request is rejected

Given an account was registered after an invitation was sent to the same address
When the invitation is accepted
Then the request returns a conflict
And the existing account and the pending invitation remain unchanged
```

```gherkin
Given an authenticated Admin and an email address without an account
When the Admin invites that address as an Agent
Then a pending invitation with a 24-hour expiry is persisted
And a purpose-specific invitation email is sent
And the response exposes invitation metadata but no token or password
```

#### Original Description

Outcome
Allow an authorized Admin to invite company Users instead of relying on unrestricted public self-registration.

Affected Layers
Domain, Application, Infrastructure, API, Tests

In Scope
Admin-only invitation creation, recipient email, role selection, expiring single-use invitation token, account activation, duplicate invitation handling, and audit-ready events.

Out of Scope
Corporate identity-provider login.

Acceptance Criteria
- Only authorized Admins can create invitations.
- The invitation is tied to one email and one allowed role.
- Expired or reused invitations are rejected.
- Completing an invitation creates or activates exactly one User.
- The invitation flow reuses the approved email delivery and token infrastructure.

Dependency
AUTH-002.

#### Imported Comments

_No comments were stored._

#### Imported Subtasks

_No subtasks were stored._

### AUTH-006 - Reset Forgotten Passwords

- Local status: `Done`
- Completed: `Yes`
- Completed at: `2026-09-10T10:59:27.994Z`
- Implementation and automated verification completed: `2026-09-07`; user review and Git publication pending.
- Source: User-approved local task, 2026-09-06.
- Dependencies: AUTH-003; reuse email delivery from AUTH-002.

#### Outcome

Let an existing password-based account recover access without a new invitation or disclosure of its old password.

#### Affected Layers

Domain, Application, Infrastructure, Api, Tests, deployment documentation.

#### Scope and Contract

Approved local decisions (2026-09-06), implemented by 2026-09-07: 30-minute reset tokens, 60-second per-account sending cooldown, 5 forgot-password requests and 10 reset-password requests per IP per 15-minute window (configurable). Unverified password accounts may recover without changing verification status. New requests return identical 202 responses; email delivery is processed outside the HTTP response using a persistent PostgreSQL work queue. Successful reset returns 204 and atomically changes the password, consumes the reset token, and increments a server-checked AuthVersion. Stale JWTs and legacy tokens without the version claim are rejected at the shared JWT validation point. No automatic login. AUTH-002 and AUTH-003 remain Review; the user explicitly approved proceeding. Request/delivery and password replacement/session invalidation were implemented as separate slices.

- POST /auth/forgot-password accepts an email address and returns the same public response for eligible and unknown accounts.
- POST /auth/reset-password accepts a short-lived, single-use password reset token and the new password.
- Password reset tokens are purpose-separated from verification and invitation tokens; persist hashes only and never log raw tokens.
- Reuse the existing password validator and hasher. Do not change roles or create new accounts.
- Define request throttling, token lifetime, and unverified-account behavior before implementation.
- Invalidate existing sessions on successful reset using an explicitly designed server-side version/revocation check; changing PasswordHash alone does not revoke JWTs.
- Perform token consumption, password update, and session invalidation atomically, including concurrent reset attempts.
- Reuse local Mailpit delivery; defer browser screens to the frontend phase.

#### Acceptance Scenarios

```gherkin
Given an eligible account whose owner forgot their password
When the owner requests recovery and submits a valid reset token with a valid new password
Then the new password works and the old password fails
And the reset token and previous sessions can no longer be used

Given an expired, reused, or wrong-purpose token
When a password reset is attempted
Then the password and account remain unchanged

Given an unknown email address
When password recovery is requested
Then the public response does not reveal whether an account exists
```

#### Test Plan and Completion

- Use vertical xUnit HTTP tests with real PostgreSQL and captured email; cover expiry, reuse, purpose isolation, throttling, and concurrent consumption.
- Verify build, regression tests, and Slopwatch; document recovery and session behavior before Review.

#### First Slice Checkpoint - 2026-09-06

- Implemented POST /auth/forgot-password with the same 202 body for known/unknown syntactically valid addresses, a PostgreSQL queue, and background delivery outside the HTTP response path.
- Added 30-minute purpose-specific pwd_ tokens stored as hashes, per-account 60-second issuance cooldown, short row-lock transactions, leased queue jobs (2 minutes), bounded delivery cancellation (30 seconds), retries after 60 seconds with at most three attempts, and stale-job retirement after 30 minutes.
- Added configurable forgot-password IP limits (default 5 per 15 minutes, per application instance). No client-supplied forwarding header is trusted implicitly. Multi-instance limiting remains a deployment concern.
- SMTP cleanup regression found by read-only review: QUIT could hang after message acceptance. Reproduced with a local SMTP protocol test; transport now disposes the connection without a second QUIT exchange.
- Evidence: 276 tests passed, none skipped; build 0 warnings/errors; Slopwatch 0 findings. Includes request privacy, actual background delivery, restart persistence, cooldown, failed-delivery retry, slow SMTP independence, and real Mailpit delivery. All multi-worker lease interleavings and the three-attempt exhaustion path were not individually forced in tests.
- First HTTP, worker delivery, limiter, and SMTP lifecycle tests had observed RED failures followed by GREEN; later cases extended regression coverage.
- Source explanation and full file snapshots: [AUTH-006 request/delivery notes](../agents/auth-006-password-recovery-request.md).
- Historical first-slice state: reset-password and AuthVersion validation were still pending at this checkpoint. Superseded by the final checkpoint below.
- Development DB update, manual Swagger checks, commit/push/tag remain pending. Test DB migrations were applied successfully. No existing password, verification state, role, or JWT was changed by the new request flow.

#### Final Slice Checkpoint - 2026-09-07

- Implemented POST /auth/reset-password with ResetPasswordRequest (token/newPassword), existing password validation/hashing, 204 on success, generic 400 for invalid/expired/used credentials, and a separate configurable reset IP budget (default 10 per 15 minutes per instance).
- Added ConsumedAtUtc and User.AuthVersion. PostgreSQL account-first row locking serializes issuance, consumption, and revocation. Password hash, consumption time, and version commit atomically; final token validity/time are checked after acquiring the lock.
- Added auth_version to new JWTs and shared JwtSessionValidationEvents. A scalar DB lookup rejects stale, missing/duplicate/malformed versions and missing accounts before protected endpoints. Legacy JWTs require login again when this code is deployed. Role and email verification state remain unchanged.
- Added migration AddPasswordResetConsumptionAndAuthVersion through EF CLI; applied successfully to disposable PostgreSQL test databases. No manual development database update in this slice.
- Read-only subagent review identified a revocation/reset race; the main agent verified and fixed RevokeAsync to use the same account-first transaction. The exact revocation/reset interleaving was statically reviewed, not individually forced in a deterministic concurrency test.
- Evidence: 290 tests passed, 0 failed/skipped; build 0 warnings/errors; Slopwatch 0 findings. Includes expiry boundary, replacement/replay, invalid password preserving token/session, concurrent resets with exactly one winner, wrong-purpose/unknown codes, failed SMTP token rejection, verified/unverified state preservation, reset throttling, and correctly signed invalid-version JWTs on /me, /admin/access, and /tickets.
- A fault-injection HTTP test throws after EF SQL writes but before transaction commit; old password/session remain valid and the same reset code can be retried successfully, proving rollback of the three reset effects.
- First reset HTTP test observed RED (404) before implementation, then GREEN; subsequent tests extend regression coverage. One full-suite attempt failed due unavailable Docker; after Docker startup the complete suite passed 290/290.
- Full method/parameter/layer explanations and source snapshots: [AUTH-006 password reset notes](../agents/auth-006-password-reset.md). Previous slice notes are historical snapshots.
- AUTH-006 is Review, not Done. Manual Swagger/frontend checks remain user-deferred; permanent development DB update and commit/push/tag remain pending. No browser reset form, refresh-token system, production SMTP deployment, or SSO added.
- NEXT: present AUTH-004 scope/contract and obtain the required implementation approval; do not silently start disabling public registration.

### AUTH-004 - Disable Public Registration in Production

- Local status: `Done`
- Completed: `Yes`
- Implementation and automated verification completed: `2026-09-07`; user review and Git publication pending.
- Original Asana section: `Backlog`
- Asana task ID: `1217558786593366`
- Created at: `2026-08-17T17:54:15.200Z`
- Modified at: `2026-08-17T17:54:44.890Z`
- Completed at: `2026-09-10T10:59:27.994Z`
- Historical Asana link: https://app.asana.com/1/1217520784789373/project/1217519861613556/task/1217558786593366

#### Approved Implementation Contract - 2026-09-07

- Registration:PublicRegistrationEnabled defaults to false; Development explicitly enables it. Production with true fails startup validation instead of silently exposing self-registration. Other non-production/demo environments may explicitly enable it.
- A disabled registration rejects a valid request with 403 before account lookup, creation, verification email, or JWT generation. Invalid HTTP JSON may still be rejected by ASP.NET Core model binding with 400.
- Guard IAuthService.RegisterAsync, not only the Controller. Preserve login, email confirmation, password recovery, admin invitations/acceptance, and admin Agent provisioning.
- Use a typed Application settings object bound and validated by Api configuration/DI; no new NuGet packages or migration. Configuration changes require application restart.
- Test boundaries: HTTP register/login/invitation/recovery, direct IAuthService public contract, and startup configuration. Start with disabled HTTP registration RED/GREEN, then explicit enablement, Production startup refusal, and preserved identity flows.

```gherkin
Given public registration is disabled
When a visitor submits valid registration details
Then the API returns 403 and no account is created

Given Development explicitly enables public registration
When a visitor submits valid registration details
Then registration succeeds with the existing verification flow

Given Production is configured to enable public registration
When the application starts
Then startup fails with an actionable configuration error

Given public registration is disabled and an admin invites a person
When that person accepts the invitation
Then the person can sign in and recover their password
```

#### Implementation Checkpoint - 2026-09-07

- Added Application RegistrationSettings and Api RegistrationConfiguration binding/validation/DI. Missing settings default false; Development explicitly opts in. Production + true and malformed boolean values fail startup. Registration options are resolved before the existing database preparation block.
- AuthService.RegisterAsync rejects disabled self-registration with ForbiddenException before account lookup, hashing, persistence, email, or JWT generation. Controller documents 403. No DTO, Domain, Infrastructure, package, or migration change in this task.
- Testing explicitly opts into registration instead of depending on Development settings. Configuration is fixed for the application lifetime; restart after changes. Deployment must correctly set its host environment; a mislabeled Development server is not automatically detected as Production.
- RED/GREEN observed: disabled register initially returned 201 instead of 403; Production + true initially started instead of throwing. Both fixed and verified. Remaining scenarios are regression coverage, not claimed as separate production RED cycles.
- Verification: 13 new cases; complete suite 303 passed, 0 failed/skipped; build 0 warnings/errors; Slopwatch 0 findings. Tests cover environment/default configuration, malformed settings, no account/email when disabled, direct IAuthService guard, existing email confirmation, admin Agent provisioning, and Production invitation -> login -> password recovery/reset -> verified Ticket access.
- Updated README deployment/local configuration guidance; full method, signature, layer and code explanations: [AUTH-004 registration policy notes](../agents/auth-004-registration-policy.md).
- User Secrets, permanent development DB and Git commit/push/tag were not changed. Manual Swagger checks remain user-deferred. Status is Review, not Done.
- NEXT: AUTH-005 requires a concrete corporate OIDC provider/tenant and identity/role policy decisions before implementation; do not invent provider credentials or silently configure SSO.

#### Original Description

Outcome
Keep self-registration available in Development/demo environments but disable it safely in Production.

Affected Layers
Application, API, Configuration, Tests, Documentation

In Scope
Strongly typed registration settings, startup validation, environment-specific behavior, predictable API response when registration is disabled, and documentation for deployment configuration.

Out of Scope
Implementing an external SSO provider.

Acceptance Criteria
- Development can explicitly enable public registration.
- Production defaults to public registration disabled.
- Disabled registration cannot create a User.
- Admin invitation and approved identity flows continue to work.
- Automated tests cover both enabled and disabled configurations.

Dependency
AUTH-003.

#### Imported Comments

_No comments were stored._

#### Imported Subtasks

_No subtasks were stored._

### AUTH-005 - Integrate Corporate SSO

- Local status: `Done`
- Completed: `Yes`
- Original Asana section: `Backlog`
- Asana task ID: `1217558786327580`
- Created at: `2026-08-17T17:54:15.182Z`
- Modified at: `2026-08-17T17:54:44.879Z`
- Completed at: `2026-09-10T10:59:27.994Z`
- Historical Asana link: https://app.asana.com/1/1217520784789373/project/1217519861613556/task/1217558786327580

#### Approved Local Design - 2026-09-07

- Local Keycloak development; user review remains deferred until the identity tasks are implemented. Automated verification is not deferred. Subagent research encountered a usage limit; main agent continues.
- First SSO sign-in requires an existing OpsDesk admin invitation matching the provider-verified email. The invitation supplies Customer/Agent role, never provider role claims. No automatic email-based linking to existing local accounts.
- Stable external identity is issuer + subject. Returning linked users sign in without another invitation. SSO-only accounts have no local password and use their identity provider for password recovery.
- Standard confidential authorization-code + PKCE flow through ASP.NET Core OIDC, exact issuer/domain allowlist, nonce/state validation, fixed callback origin, no raw access token in URLs. Browser bootstrap is minimal; final product frontend remains deferred.
- Account creation, external identity link and invitation acceptance must commit atomically. Local password login and existing JWT policies remain supported.
- Logout revokes OpsDesk sessions through AuthVersion; provider logout is a separate browser redirect and must be documented distinctly. SSO is disabled unless explicitly configured, with HTTPS required outside local Development/Testing.
- Public test boundaries: IExternalSignInService for invitation/account transactions; HTTP OIDC challenge/callback and authenticated APIs for transport/auth behavior; actual local Keycloak browser flow for end-to-end verification.

#### Implementation Progress - 2026-09-07

- Account-provisioning slice implemented: stable issuer/subject identity, invitation-owned role, verified-email/domain checks, no automatic local-account linking, passwordless SSO account and atomic invitation consumption.
- Added `20260907152235_AddExternalUserIdentities` through EF CLI with explicit migration permission. Applied only to disposable integration-test PostgreSQL; persistent development database was not updated.
- Verification: all 313 tests passed, 0 skipped; build 0 warnings/errors; Slopwatch 0 issues. Ten external-sign-in test cases cover initial/returning sign-in, rejected identities, concurrent acceptance, local-account collision and disabled defaults.
- TDD evidence: the initial test previously failed because IExternalSignInService was absent and now passes. Additional negative cases passed as regression tests; no separate red result is claimed for those cases.
- Detailed method explanations and complete source snapshots: `docs/agents/auth-005-sso-account-provisioning.md`.
- OIDC HTTP transport is implemented with the official ASP.NET Core handler, authorization code + S256 PKCE, state/nonce/correlation validation, fixed callback origin, anti-forgery protected invitation bootstrap, and no provider-token persistence.
- Local Keycloak 26.7.3 setup imports an `opsdesk` realm, confidential client and generated-password demo user through the opt-in Compose `sso` profile. Secrets are generated into ignored `.env` and .NET User Secrets; SSO remains disabled by default.
- Logout increments AuthVersion atomically so all existing OpsDesk JWTs fail immediately, then returns a separate discovery-based provider logout URL. Provider unavailability cannot undo local logout.
- RED/GREEN evidence: browser start was initially 404, form submit 405, callback 401 because of the local correlation-cookie transport decision, and logout 404. The implemented endpoints/configuration fixed those exact failures.
- Verification: focused SSO package 21/21; complete suite 324/324 with 0 skipped; build 0 warnings/errors; Slopwatch 0 issues. Real local Keycloak browser flow reached the callback, atomically created the invited verified passwordless Agent/link in disposable PostgreSQL, found the same link on return, revoked a real JWT (`/me` 200 -> 401), and completed provider logout back to the fixed signed-out page.
- Persistent development PostgreSQL was not migrated or used for the browser run. The temporary `--rm` database was removed. Keycloak and Mailpit remain available for user review. Manual Swagger/user review and Git commit/push/tag remain pending.
- Full protocol, method, parameter, layer, setup and test explanations: `docs/agents/auth-005-sso-browser-flow.md`.

```gherkin
Given an admin invitation matching a verified corporate identity
When its owner first signs in through the approved provider
Then one passwordless OpsDesk account is created with the invitation role
And the invitation is consumed and the external identity is linked

Given an external identity without an accepted link or matching invitation
When it attempts SSO sign-in
Then no OpsDesk account or access token is created

Given a previously linked external identity
When it signs in again
Then it accesses the same OpsDesk account without another invitation
```

#### Original Description

Outcome
Authenticate production Users through a corporate OpenID Connect identity provider while preserving OpsDesk roles and authorization policies.

Affected Layers
Domain, Application, Infrastructure, API, Tests, Deployment documentation

In Scope
Provider-neutral OIDC integration, external identity mapping, local User provisioning/linking rules, role mapping, logout behavior, configuration validation, and integration tests.

Out of Scope
Supporting several identity providers in the first implementation and replacing JWT for API authorization unless the design requires it.

Acceptance Criteria
- An approved corporate identity can sign in without an OpsDesk password.
- External identities map deterministically to one local User.
- Unapproved tenants/domains are rejected.
- Existing policy-based authorization continues to enforce OpsDesk roles.
- Production documentation explains provider configuration and secret handling.

Dependencies
AUTH-003 and AUTH-004.

#### Imported Comments

_No comments were stored._

#### Imported Subtasks

_No subtasks were stored._

### OPS-001 - Configure the V2 Delivery Workflow

- Local status: `Done`
- Completed: `Yes`
- Original Asana section: `Done`
- Asana task ID: `1217550831985653`
- Created at: `2026-08-17T14:57:17.378Z`
- Modified at: `2026-08-17T15:11:02.794Z`
- Completed at: `2026-08-17T15:10:30.231Z`
- Historical Asana link: https://app.asana.com/1/1217520784789373/project/1217519861613556/task/1217550831985653

#### Original Description

Outcome
Establish the shared workflow used to select, approve, implement, review, and complete V2 work.

Affected Layers
Workflow and Asana

In Scope
Use Backlog, Ready, In Progress, Review, and Done; enforce WIP=1; define task-fetching and approval rules; use the standard task body.

Out of Scope
Product code, database changes, and release work.

Public Seam
Asana project workflow.

Dependencies
None.

Gherkin Scenarios
Not applicable. This task defines how later tasks carry Gherkin scenarios.

Workflow Configuration
- Backlog: Approved roadmap items that are not yet ready to implement.
- Ready: Tasks with resolved decisions, completed dependencies, an agreed public seam, and no open product question.
- In Progress: The single task currently being implemented.
- Review: Implementation is complete and build/test evidence is ready for user review.
- Done: The user has accepted the result and required closing operations are complete.
- WIP limit: In Progress may contain at most one task. If more than one task is present, the agent stops and reports the violation.
- Fetch order: Continue the single In Progress task first. Otherwise select the highest-priority unblocked Ready task. Backlog tasks are not implemented until they are made Ready or the user explicitly selects one.
- Approval boundary: Ready status does not grant permission to edit files, packages, migrations, databases, Docker, Git, or Asana. The task-specific approval rules in AGENTS.md still apply.

Standard Task Body
Every implementation task must contain Outcome, Affected Layers, In Scope, Out of Scope, Public Seam, Dependencies, Gherkin Scenarios, Test Plan, and Definition of Done.

Test Plan
- Confirm the required sections exist.
- Confirm In Progress contains no more than one task.
- Confirm V2 tasks use the standard body.
- Confirm dependencies are represented as Asana dependencies, not only as text.
- Confirm the fetch order agrees with docs/agents/testing-workflow.md and AGENTS.md.

Definition of Done
- The workflow and standard task body are visible in Asana.
- WIP=1 and the task-fetch order are explicitly documented.
- V2 task dependencies are linked in Asana.
- The workflow agrees with AGENTS.md and docs/agents/testing-workflow.md.
- No product code or database state is changed.

#### Imported Comments

_No comments were stored._

#### Imported Subtasks

_No subtasks were stored._

### OPS-002 - Adopt BDD, TDD, and Agent Delegation Rules

- Local status: `Done`
- Completed: `Yes`
- Original Asana section: `Done`
- Asana task ID: `1217550926266388`
- Created at: `2026-08-17T14:57:17.390Z`
- Modified at: `2026-08-17T15:17:56.340Z`
- Completed at: `2026-08-17T15:17:56.300Z`
- Historical Asana link: https://app.asana.com/1/1217520784789373/project/1217519861613556/task/1217550926266388

#### Original Description

Outcome
Make the approved BDD/TDD cycle and subagent governance binding for V2 work.

Affected Layers
AGENTS.md and docs/agents/testing-workflow.md

In Scope
Keep Gherkin scenarios in Asana, executable tests in xUnit, work one red-green slice at a time, and constrain subagent use.

Out of Scope
Reqnroll, .feature files, production feature code, and Asana automation code.

Public Seam
Repository working instructions.

Dependencies
OPS-001.

Gherkin Scenarios
Not applicable. The document defines how scenarios will be created when implementation tasks are fetched.

Test Plan
Review the written rules for approval gating, public-seam testing, and non-overlapping subagent work.

Implementation Evidence
- AGENTS.md contains binding Subagent Usage rules.
- AGENTS.md contains the approved BDD and TDD workflow.
- docs/agents/testing-workflow.md keeps Gherkin scenarios in Asana and executable tests in xUnit.
- The workflow defines vertical red-green slices, public-seam testing, coupling rules, and Definition of Done.
- Reqnroll and .feature files were not introduced.
- No production code, package, migration, database, Docker, or Git operation was changed by this task.

Definition of Done
Both documents contain the approved rules and no unrelated files are changed.

#### Imported Comments

_No comments were stored._

#### Imported Subtasks

_No subtasks were stored._

### V2-D01 - Define the Ticket Domain Vocabulary and Attributes

- Local status: `Done`
- Completed: `Yes`
- Original Asana section: `Done`
- Asana task ID: `1217550691747045`
- Created at: `2026-08-17T14:57:17.376Z`
- Modified at: `2026-08-17T15:19:01.177Z`
- Completed at: `2026-08-17T15:19:01.044Z`
- Historical Asana link: https://app.asana.com/1/1217520784789373/project/1217519861613556/task/1217550691747045

#### Original Description

Outcome
Agree on what a Ticket represents and which data belongs to it before implementation begins.

Affected Layers
Domain, Application, Tests

In Scope
Define required, optional, and server-owned attributes; title and description limits; priority and status values; requester, assignee, and timestamps.

Out of Scope
Database migration, endpoint implementation, comments, assignment workflow, and SLA fields.

Public Seam
Approved Ticket contract and glossary.

Dependencies
OPS-002.

Gherkin Scenarios
Scenario: Ticket uses server-owned defaults
  Given an authenticated User who is allowed to create Tickets
  When the User creates a Ticket with a valid title and description
  Then the Ticket has Open status
  And the Ticket has Medium priority
  And the authenticated User is the requester
  And the Ticket is unassigned

Scenario: Blank Ticket content is rejected
  Given an authenticated User who is allowed to create Tickets
  When the User creates a Ticket with a blank title or description
  Then the Ticket is rejected
  And no Ticket is created

Test Plan
No production test. Review the contract against expected create, view, and lifecycle flows.

Approved Decisions
- Title and Description are required; Priority is optional and defaults to Medium.
- Id, Status, RequesterId, AssigneeId, and timestamps are server-owned.
- Status starts as Open and AssigneeId starts as null.
- V2 uses Guid identifiers without a separate human-readable Ticket number.
- Title is limited to 200 characters and Description to 5000 characters.
- Ticket text accepts Unicode and preserves Description line breaks.
- Role permissions remain in V2-D02 and lifecycle transitions remain in V2-D03.

Documentation
- CONTEXT.md defines the canonical OpsDesk support language.
- docs/specs/v2-ticket-domain.md defines attribute ownership, nullability, limits, enum values, and deferred decisions.

Definition of Done
Field ownership, nullability, limits, enums, terminology, and unresolved questions are explicitly approved.

#### Imported Comments

_No comments were stored._

#### Imported Subtasks

_No subtasks were stored._

### V2-D02 - Define Ticket Creation and Visibility Rules

- Local status: `Done`
- Completed: `Yes`
- Original Asana section: `Done`
- Asana task ID: `1217550831913387`
- Created at: `2026-08-17T14:57:17.374Z`
- Modified at: `2026-08-17T15:20:14.439Z`
- Completed at: `2026-08-17T15:20:14.302Z`
- Historical Asana link: https://app.asana.com/1/1217520784789373/project/1217519861613556/task/1217550831913387

#### Original Description

Outcome
Decide who can create Tickets and which Tickets each role is allowed to discover or view.

Affected Layers
Domain, Application, API, Tests

In Scope
Define Customer, Agent, and Admin permissions; own-versus-other visibility; and 401, 403, and 404 information-hiding behavior.

Out of Scope
Endpoint code, persistence implementation, comments, assignment, and filtering.

Public Seam
Authorization and visibility contract.

Dependencies
V2-D01.

Gherkin Scenarios
Scenario: Authenticated User creates a Ticket for themselves
  Given an authenticated Customer, Agent, or Admin
  When the User creates a valid Ticket
  Then the authenticated User is the requester
  And the API does not accept a different requester

Scenario: Customer can view only an own Ticket
  Given two Customers have separate Tickets
  When one Customer requests the other Customer's Ticket
  Then the API returns 404 Not Found
  And the API does not reveal whether the protected Ticket exists

Scenario: Support staff can view any Ticket
  Given a Ticket requested by a Customer
  When an Agent or Admin requests that Ticket
  Then the API returns the Ticket details

Scenario: Anonymous caller cannot access Tickets
  Given a caller without valid authentication
  When the caller creates or requests a Ticket
  Then the API returns 401 Unauthorized

Test Plan
Plan authorization unit tests and API acceptance tests without binding them to policy implementation details.

Approved Decisions
- Every authenticated User can create a Ticket for themselves.
- V2 does not support creating a Ticket on behalf of another User.
- Customers discover and view only Tickets where they are the Requester.
- Agents and Admins discover and view all Tickets, including unassigned Tickets.
- Missing authentication returns 401.
- Unknown Tickets and Tickets outside a Customer's visibility both return 404.
- 403 is reserved for forbidden actions on a Ticket the caller is allowed to know exists.
- Visibility is enforced by Application use cases and visibility-aware queries, not reconstructed in controllers.

Documentation
- docs/specs/v2-ticket-access.md contains the approved role matrix and HTTP outcomes.
- docs/specs/v2-ticket-domain.md links to the access contract.

Definition of Done
The role matrix and HTTP outcomes are approved with no unresolved visibility rule.

#### Imported Comments

_No comments were stored._

#### Imported Subtasks

_No subtasks were stored._

### V20-001 - Create a Ticket End to End

- Local status: `Done`
- Completed: `Yes`
- Original Asana section: `Done`
- Asana task ID: `1217550811485719`
- Created at: `2026-08-17T14:57:17.368Z`
- Modified at: `2026-08-17T17:30:45.440Z`
- Completed at: `2026-08-17T17:30:45.268Z`
- Historical Asana link: https://app.asana.com/1/1217520784789373/project/1217519861613556/task/1217550811485719

#### Original Description

Outcome
Allow an approved authenticated role to create a Ticket through the API and persist it in PostgreSQL.

Affected Layers
Domain, Application, Infrastructure, API, Tests

In Scope
Ticket entity and enums, creation use case and DTOs, EF configuration and migration, repository behavior, POST endpoint, tests, and OpenAPI contract.

Out of Scope
Ticket listing, comments, assignment, status changes, SLA, audit logs, and AI triage.

Public Seam
POST /tickets.

Dependencies
V2-D01 and V2-D02. Both dependencies are complete.

Gherkin Scenarios
Scenario Outline: Authenticated User creates a Ticket with server-owned defaults
  Given an authenticated <role>
  When the User creates a Ticket with a valid title and description without a priority
  Then the API returns 201 Created
  And the response identifies the created Ticket
  And the Ticket has Open status
  And the Ticket has Medium priority
  And the authenticated User is the requester
  And the Ticket is unassigned
  Examples:
    | role |
    | Customer |
    | Agent |
    | Admin |

Scenario: Authenticated User creates a Ticket with an explicit priority
  Given an authenticated User
  When the User creates a valid Ticket with High priority
  Then the API returns 201 Created
  And the Ticket has High priority

Scenario Outline: Invalid Ticket content is rejected
  Given an authenticated User
  When the User creates a Ticket with <invalidField>
  Then the API returns 400 Bad Request
  And no Ticket is created
  Examples:
    | invalidField |
    | a blank title |
    | a title longer than 200 characters |
    | a blank description |
    | a description longer than 5000 characters |

Test Plan
- Start with one failing API acceptance test through POST /tickets.
- Add focused Domain unit tests only for Ticket creation invariants.
- Add API tests for role coverage, authentication, invalid input, and response contract.
- Add PostgreSQL integration tests for persistence, enum values, foreign keys, and migration behavior.
- Verify the created Ticket can be retrieved through the approved test seam without asserting internal method calls.

Implementation Evidence
- The first API acceptance test failed with 404 before POST /tickets existed, then passed after implementation.
- Ticket.Create owns normalization, default priority, initial status, requester, identifier, and UTC timestamp invariants.
- POST /tickets requires authentication and derives RequesterId from the JWT NameIdentifier claim.
- Customer, Agent, and Admin creation scenarios pass.
- Blank and overlong text, anonymous access, unsupported priority strings, and undefined Domain priority values are rejected.
- Migration 20260817161155_AddTickets creates the tickets table, indexes, and requester/assignee foreign keys.
- PostgreSQL Testcontainers verifies migration application, Ticket persistence, and requester foreign-key enforcement.
- Swagger discovers POST /tickets and enum values use snake_case strings.
- README documents the V2 creation slice and the current 40-test suite.
- dotnet build OpsDesk.sln --no-restore completed with 0 warnings and 0 errors.
- dotnet test OpsDesk.sln --no-restore completed with 40 passed, 0 failed, and 0 skipped.
- dotnet format OpsDesk.sln --no-restore --verify-no-changes passed.
- No disabled tests, warning suppressions, or arbitrary test delays were introduced.

Definition of Done
Approved scenarios pass, the migration works on clean PostgreSQL, full tests and build pass, and documentation matches the endpoint.

#### Imported Comments

_No comments were stored._

#### Imported Subtasks

_No subtasks were stored._

### V20-002 - View Ticket Details End to End

- Local status: `Done`
- Completed: `Yes`
- Original Asana section: `Done`
- Asana task ID: `1217550693885230`
- Created at: `2026-08-17T14:57:17.385Z`
- Modified at: `2026-08-20T18:14:36.302Z`
- Completed at: `2026-08-20T18:14:36.200Z`
- Historical Asana link: https://app.asana.com/1/1217520784789373/project/1217519861613556/task/1217550693885230

#### Original Description

Outcome
Allow an authorized caller to retrieve one Ticket without exposing protected information.

Affected Layers
Application, Infrastructure, API, Tests, Documentation

In Scope
Read use case and response DTO reuse, efficient PostgreSQL queries, GET endpoint, visibility enforcement, OpenAPI metadata, tests, and README updates.

Out of Scope
Collections, comments, assignment, status history, and admin audit data.

Public Seam
GET /tickets/{id}.

Dependencies
V20-001.

Gherkin Scenarios
Scenario: Requester views their own Ticket
  Given an authenticated Customer requested a Ticket
  When the Customer requests GET /tickets/{id}
  Then the API returns 200 OK
  And the approved Ticket fields are returned

Scenario: Customer cannot discover another requester's Ticket
  Given a Ticket requested by another Customer
  When the authenticated Customer requests GET /tickets/{id}
  Then the API returns 404 Not Found

Scenario Outline: Support staff views any Ticket
  Given a Ticket requested by a Customer
  And an authenticated <role>
  When the support User requests GET /tickets/{id}
  Then the API returns 200 OK
  Examples:
    | role |
    | Agent |
    | Admin |

Scenario: Authenticated User requests an unknown Ticket
  Given an authenticated User
  When the User requests an unknown Ticket identifier
  Then the API returns 404 Not Found

Scenario: Anonymous caller requests Ticket details
  Given no valid authentication
  When the caller requests GET /tickets/{id}
  Then the API returns 401 Unauthorized

Implementation Evidence
- TicketService owns the Customer versus Agent/Admin visibility decision.
- Customer reads use a requester-scoped query; Agent/Admin reads use the unrestricted ID query.
- Both EF Core queries use AsNoTracking and execute visibility filtering in PostgreSQL.
- Hidden and unknown Tickets both return 404 to prevent resource discovery.
- The existing TicketResponse contract is reused, so no User profile or protected authentication data is exposed.
- Swagger/OpenAPI documents 200, 401, and 404 outcomes.
- README was shortened into stable capability, stack, architecture, endpoint, local setup, admin seed, test, design, and roadmap sections.
- dotnet format OpsDesk.sln --no-restore --verify-no-changes passed.
- dotnet build OpsDesk.sln --no-restore passed with 0 warnings and 0 errors.
- dotnet test OpsDesk.sln --no-restore passed with 46 passed, 0 failed, and 0 skipped.
- Slopwatch was unavailable locally; an equivalent current-diff scan found no disabled tests, warning suppressions, empty catches, or arbitrary delays.

Definition of Done
The approved fields and HTTP outcomes are stable, visibility is enforced, and the full test suite passes.

#### Imported Comments

_No comments were stored._

#### Imported Subtasks

_No subtasks were stored._

### V2-D03 - Define the Ticket Lifecycle and Status History Contract

- Local status: `Done`
- Completed: `Yes`
- Original Asana section: `Done`
- Asana task ID: `1217550627666090`
- Created at: `2026-08-17T14:57:17.375Z`
- Modified at: `2026-08-20T18:15:47.749Z`
- Completed at: `2026-08-20T18:15:47.370Z`
- Historical Asana link: https://app.asana.com/1/1217520784789373/project/1217519861613556/task/1217550627666090

#### Original Description

Outcome
Define valid Ticket status transitions and the history that authorized users can observe.

Affected Layers
Domain, Application, API, Tests

Public Seam
Approved lifecycle contract in docs/specs/v2-ticket-lifecycle.md and PATCH /tickets/{id}/status for implementation.

Approved Decisions
- Customers can only reopen or close their own resolved Tickets.
- Agents and Admins manage operational transitions for visible Tickets.
- Allowed operational flow is open -> in_progress -> waiting_customer/resolved -> closed, with waiting_customer -> in_progress and resolved -> in_progress reopening paths.
- Closed is terminal in V2.
- Assignment is not required for transitions in V2.
- Hidden Tickets return 404, forbidden visible operations return 403, malformed status returns 400, and invalid/no-op transitions return 409.
- Successful transitions update the Ticket and create exactly one user-visible status-change record in one database transaction.
- History exposes actor, previous status, new status, and UTC timestamp without becoming an admin audit log.

Gherkin Coverage
Representative allowed Agent transition, forbidden Customer transition, invalid Agent transition, and requester reopening scenarios are recorded in the lifecycle contract.

Test Plan
Domain matrix tests, API acceptance tests through PATCH /tickets/{id}/status, and PostgreSQL atomic persistence tests.

Definition of Done
Every status has explicit allowed transitions, actors, failure behavior, and visible history fields.

#### Imported Comments

_No comments were stored._

#### Imported Subtasks

_No subtasks were stored._

### V20-003 - Change Ticket Status End to End

- Local status: `Done`
- Completed: `Yes`
- Original Asana section: `Done`
- Asana task ID: `1217550810686229`
- Created at: `2026-08-17T14:57:17.366Z`
- Modified at: `2026-08-24T16:26:10.112Z`
- Completed at: `2026-08-21T14:39:23.944Z`
- Historical Asana link: https://app.asana.com/1/1217520784789373/project/1217519861613556/task/1217550810686229

#### Original Description

Outcome
Change Ticket status only when the approved lifecycle and role rules allow it.

Affected Layers
Domain, Application, Infrastructure, API, Tests

In Scope
Domain transition rules, authorization orchestration, atomic status and event persistence, endpoint, and tests.

Out of Scope
Comments, assignment events, SLA processing, and admin audit log.

Public Seam
PATCH /tickets/{id}/status

Implemented Behavior
- Agent and Admin can use the approved operational transition matrix.
- Customer can reopen or close only their own resolved Ticket.
- Closed is terminal.
- Hidden Ticket returns 404, forbidden visible operation returns 403, malformed status returns 400, and invalid/no-op transition returns 409.
- Every successful transition stores exactly one TicketStatusChange with actor, previous status, new status, and UTC timestamp.
- Ticket and status-history changes are persisted by one EF Core SaveChanges transaction; a PostgreSQL foreign-key failure test verifies both changes roll back together.

Gherkin Evidence
Scenario: Allowed transition
Given an open Ticket and an authenticated Agent
When the Agent changes the status to in_progress
Then the API returns 200
And the Ticket and one status-change event are persisted atomically

Scenario: Forbidden role
Given a Customer owns an open Ticket
When the Customer changes the status to in_progress
Then the API returns 403
And no Ticket or status-history change is persisted

Scenario: Invalid transition
Given an open Ticket and an authenticated Agent
When the Agent changes the status directly to resolved
Then the API returns 409
And no Ticket or status-history change is persisted

Key Files
- src/OpsDesk.Domain/Entities/Ticket.cs
- src/OpsDesk.Domain/Entities/TicketStatusChange.cs
- src/OpsDesk.Application/Tickets/Services/TicketService.cs
- src/OpsDesk.Api/Controllers/TicketsController.cs
- src/OpsDesk.Infrastructure/Persistence/Configurations/TicketStatusChangeConfiguration.cs
- tests/OpsDesk.Tests/Integration/TicketStatusIntegrationTests.cs

Verification
- dotnet format OpsDesk.sln --verify-no-changes --no-restore: passed
- dotnet build OpsDesk.sln --no-restore: 0 warnings, 0 errors
- dotnet test OpsDesk.sln --no-build --no-restore: 69 passed, 0 failed
- Slopwatch-equivalent scan: no hand-written slop patterns; only EF-generated migration warning pragmas

Definition of Done
Invalid or forbidden operations make no database change; allowed transitions and their events persist atomically; all tests pass.

#### Imported Comments

_No comments were stored._

#### Imported Subtasks

_No subtasks were stored._

### V20-004 - View Ticket Status History

- Local status: `Done`
- Completed: `Yes`
- Original Asana section: `In Progress`
- Asana task ID: `1217550484788434`
- Created at: `2026-08-17T14:57:17.378Z`
- Modified at: `2026-08-21T15:35:01.362Z`
- Completed at: `2026-08-21T15:35:01.041Z`
- Historical Asana link: https://app.asana.com/1/1217520784789373/project/1217519861613556/task/1217550484788434

#### Original Description

Outcome
Show authorized users who changed a Ticket status, when it changed, and the before-and-after states.

Affected Layers
Application, Infrastructure, API, Tests

In Scope
History response contract, deterministic chronological query, endpoint or Ticket-details inclusion, visibility enforcement, and tests.

Out of Scope
Comment and assignment activity, SLA events, and internal admin audit data.

Public Seam
The history endpoint or Ticket-details contract approved in V2-D03.

Dependencies
V20-003.

Gherkin Scenarios
Generate scenarios for visible chronological history and a caller without Ticket visibility.

Test Plan
API acceptance tests and deterministic PostgreSQL ordering tests.

Definition of Done
History returns actor, from, to, and time in stable order without leaking internal-only data.

#### Imported Comments

_No comments were stored._

#### Imported Subtasks

_No subtasks were stored._

### REL-200 - Validate and Release v2.0.0

- Local status: `Done`
- Completed: `Yes`
- Original Asana section: `Done`
- Asana task ID: `1217550831134012`
- Created at: `2026-08-17T14:57:17.409Z`
- Modified at: `2026-08-24T15:35:46.734Z`
- Completed at: `2026-08-24T15:35:46.616Z`
- Historical Asana link: https://app.asana.com/1/1217520784789373/project/1217519861613556/task/1217550831134012

#### Original Description

Outcome
Verify the complete core Ticket lifecycle before creating the v2.0.0 release.

Affected Layers
Domain, Application, Infrastructure, API, Tests, Documentation

In Scope
Build, full tests, clean PostgreSQL migration, Swagger flow, README updates, secret and diff checks, release notes.

Out of Scope
Comments, assignment, collection queries, Git push, and tag creation without separate approval.

Public Seam
Public v2.0.0 API contract.

Dependencies
V20-001, V20-002, V20-003, and V20-004.

Gherkin Scenarios
Re-run all approved v2.0.0 task scenarios.

Test Plan
Full unit, integration, API acceptance, migration, and coverage verification.

Definition of Done
Release evidence is recorded; commit, push, and tag remain separately approved operations.

#### Imported Comments

_No comments were stored._

#### Imported Subtasks

_No subtasks were stored._

### V21-D01 - Define Ticket Collaboration Rules

- Local status: `Done`
- Completed: `Yes`
- Original Asana section: `Done`
- Asana task ID: `1217550656667788`
- Created at: `2026-08-17T14:57:17.388Z`
- Modified at: `2026-08-24T16:26:10.239Z`
- Completed at: `2026-08-24T16:26:10.114Z`
- Historical Asana link: https://app.asana.com/1/1217520784789373/project/1217519861613556/task/1217550656667788

#### Original Description

Outcome
Approved how public comments, Ticket assignment, and resolution confirmation work before collaboration endpoints are implemented.

Approved Comment Contract
- A Customer can comment only on their own visible Ticket.
- Agent and Admin users can comment on any visible Ticket.
- All V2.1 comments are public; internal notes are out of scope.
- Closed Tickets are read-only. Resolved Tickets may receive comments without an implicit status change.
- Content is trimmed, required, and limited to 4,000 characters.
- Author identity and UTC creation time are server-owned.
- Comment editing and deletion are out of scope.

Approved Assignment Contract
- A Customer cannot assign or unassign Tickets.
- An Agent can self-assign an unassigned Ticket and remove only their own assignment.
- An Agent cannot assign another Agent, take another Agent's Ticket, or remove another Agent's assignment.
- An Admin can assign, reassign, and unassign.
- Only an existing UserRole.Agent is a valid target. Active/inactive Agent state is not modeled yet.
- Closed Tickets cannot change assignment.
- Repeating the same assignment or unassignment is idempotent and creates no duplicate activity.
- Public endpoints are PUT /tickets/{id}/assignee and DELETE /tickets/{id}/assignee.

Approved Resolution Contract
- Agent and Admin users can resolve but cannot close Tickets.
- The requester can confirm closure or reopen their own resolved Ticket.
- Reopening requires a reason and will later persist the status change and public comment atomically.
- Closed is final in V2.1. Formal appeals, automatic closure, reopen windows, and follow-up Tickets remain future work.

Affected Layers
Domain, Application, API, Tests

Out of Scope
Endpoint and database implementation, notifications, SLA, admin audit logs, formal appeals, and background jobs.

Definition of Done
Comment visibility, assignment permissions, valid targets, and requester-confirmed closure have no unresolved role or ownership decisions.

#### Imported Comments

_No comments were stored._

#### Imported Subtasks

_No subtasks were stored._

### V21-001 - Add a Comment to a Ticket

- Local status: `Done`
- Completed: `Yes`
- Original Asana section: `Done`
- Asana task ID: `1217550692617024`
- Created at: `2026-08-17T14:57:17.379Z`
- Modified at: `2026-08-24T17:05:23.164Z`
- Completed at: `2026-08-24T17:05:23.040Z`
- Historical Asana link: https://app.asana.com/1/1217520784789373/project/1217519861613556/task/1217550692617024

#### Original Description

Outcome
Allow an authorized participant to respond publicly to a Ticket.

Status
Implemented and moved to Review. The requester-confirmed closure rule was also enforced: Agent and Admin users can resolve but cannot close Tickets.

Affected Layers
Domain, Application, Infrastructure, API, Tests

Implemented Contract
- POST /tickets/{id}/comments returns 201 with the persisted public Comment.
- Request DTO accepts Content only; Ticket ID comes from the route, Author ID and role from JWT claims, and creation time from the server.
- Content is trimmed, required, and limited to 4,000 characters.
- A Customer can comment only on their own Ticket. Agent and Admin users can comment on any visible Ticket.
- Closed Tickets reject comments with Conflict. Resolved Tickets accept comments without an implicit status change.
- Ticket UpdatedAtUtc and the new Comment persist in one SaveChanges transaction.
- TicketComment rows provide the durable activity data that V21-004 will project; no duplicate generic event row is written.

Persistence
Added ticket_comments with Ticket and Author foreign keys plus deterministic Ticket/time/ID ordering index. EF migration: AddTicketComments.

Verification
- Comment Domain and API scenarios: 16 passed.
- Ticket lifecycle integration scenarios: 16 passed.
- Full solution: 91 passed, 0 failed, 0 skipped.
- dotnet format verification passed.
- EF reports no pending model changes.

Out of Scope
Editing or deleting comments, internal notes, attachments, notifications, automatic status changes, formal appeals, and AI summaries.

Definition of Done
Implementation and automated verification are complete. Awaiting user review before Done.

#### Imported Comments

_No comments were stored._

#### Imported Subtasks

_No subtasks were stored._

### V21-002 - View Ticket Comments

- Local status: `Done`
- Completed: `Yes`
- Original Asana section: `Done`
- Asana task ID: `1217550693650708`
- Created at: `2026-08-17T14:57:17.375Z`
- Modified at: `2026-08-26T15:48:08.801Z`
- Completed at: `2026-08-26T15:48:08.660Z`
- Historical Asana link: https://app.asana.com/1/1217520784789373/project/1217519861613556/task/1217550693650708

#### Original Description

Outcome
Authorized viewers can retrieve a Ticket's public conversation through a dedicated endpoint in deterministic chronological order.

Implemented Public Seam
GET /tickets/{id}/comments

Response Contract
Returns an array of the existing TicketCommentResponse contract:
- id
- ticketId
- authorId
- content
- createdAtUtc

Authorization and Visibility
- A Customer can read Comments only for their own Ticket.
- Agent and Admin can read Comments for any visible Ticket.
- Another Customer receives 404 so protected Ticket existence is not disclosed.
- Anonymous callers receive 401.
- A visible Ticket without Comments returns 200 with [].
- Closed Tickets remain readable even though they reject new Comments.

Ordering
PostgreSQL orders by created_at_utc ascending and then id ascending. The existing composite index supports this deterministic query.

Affected Layers
- Application: ITicketService, ITicketRepository, TicketService
- Infrastructure: TicketRepository
- API: TicketsController
- Tests: TicketCommentIntegrationTests
- Documentation: README

Design Notes
- The existing TicketCommentResponse DTO is reused.
- AuthorId remains the public author identity for V2.1.
- No Domain change or EF Core migration was required.
- Shared read-only Ticket visibility selection was extracted into a private Application helper; write operations still use tracked queries.

Acceptance Scenarios
- Given a requester with Comments, when they read the conversation, then Comments are returned oldest first.
- Given a visible Ticket without Comments, when it is read, then 200 and [] are returned.
- Given another Customer, when they request the conversation, then 404 is returned.
- Given an Agent or Admin, when they request a visible conversation, then 200 is returned.
- Given a Closed Ticket, when its requester reads the conversation, then the existing Comments remain available.
- Given equal Comment timestamps, then Comment ID provides stable ordering.
- Given an anonymous caller, then 401 is returned.

Verification
- Ticket Comment integration tests: 18 passed.
- Full solution: 99 passed, 0 failed, 0 skipped.
- dotnet format --verify-no-changes passed.
- git diff --check found no whitespace errors.
- Manual Slopwatch-equivalent scan found no new shortcut patterns.

Status
Implementation complete and awaiting user review. No commit or push has been performed.

#### Imported Comments

_No comments were stored._

#### Imported Subtasks

_No subtasks were stored._

### V21-D02 - Define Agent Ticket Visibility and Ownership

- Local status: `Done`
- Completed: `Yes`
- Original Asana section: `Done`
- Asana task ID: `1217871886846367`
- Created at: `2026-08-26T16:00:12.734Z`
- Modified at: `2026-08-26T16:00:32.436Z`
- Completed at: `2026-08-26T16:00:13.017Z`
- Historical Asana link: https://app.asana.com/1/1217520784789373/project/1217519861613556/task/1217871886846367

#### Original Description

Outcome
Define row-level Ticket visibility and ownership rules so Agents cannot discover or operate on another Agent's work.

Approved Visibility Contract
- Customer: can read only Tickets they requested.
- Agent: can read unassigned Tickets and Tickets assigned to themselves.
- Admin: can read every Ticket and its assignee.
- A Ticket assigned to another Agent is hidden with 404 across details, Comments, status history, status changes, and assignment operations.

Approved Action Contract
- Agent must self-assign an unassigned Ticket before adding Agent Comments or changing status.
- Agent can self-assign only an unassigned Ticket.
- Agent can unassign only their own Ticket.
- Admin can assign, reassign, unassign, comment, and change status on any non-Closed Ticket.
- Customer behavior remains requester-scoped.
- Assignment never changes Ticket status automatically.

Concurrency Contract
- If two Agents race to claim one unassigned Ticket, exactly one succeeds.
- The losing request returns 409 without overwriting ownership or creating activity.
- Assignment state and assignment activity persist atomically.

Idempotency Contract
- Assigning the same Agent again is a successful no-op.
- Removing an already empty assignment is a successful no-op.
- No-op operations do not change UpdatedAtUtc and do not create duplicate activity.

Affected Capabilities
Ticket details, Comment reads and writes, status history, status transitions, assignment endpoints, and future Ticket collection queries.

Definition of Done
The decision is recorded, V21-003 depends on it, and implementation tests enforce both row visibility and action ownership.

#### Imported Comments

_No comments were stored._

#### Imported Subtasks

_No subtasks were stored._

### V21-003 - Assign or Unassign a Ticket

- Local status: `Done`
- Completed: `Yes`
- Original Asana section: `Done`
- Asana task ID: `1217550809783869`
- Created at: `2026-08-17T14:57:17.435Z`
- Modified at: `2026-08-27T12:55:02.990Z`
- Completed at: `2026-08-27T12:55:02.862Z`
- Historical Asana link: https://app.asana.com/1/1217520784789373/project/1217519861613556/task/1217550809783869

#### Original Description

Outcome
Let approved roles manage Agent ownership without corrupting existing assignment, while preventing Agents from discovering another Agent's work.

Affected Layers
Domain, Application, Infrastructure, API, Tests

In Scope
Agent self-assignment, Admin assignment and reassignment, authorized unassignment, existing UserRole.Agent target validation, Closed-Ticket protection, idempotent no-op behavior, concurrency protection, atomic persistence, assignment activity data, row-level Agent visibility, endpoints, and tests.

Out of Scope
Teams, active/inactive Agent state, workload balancing, automatic routing, notifications, and AI triage.

Public Seam
- PUT /tickets/{id}/assignee with an assigneeId request.
- DELETE /tickets/{id}/assignee.

Visibility Contract
- Customer: requester's own Tickets.
- Agent read: unassigned Tickets or Tickets assigned to that Agent.
- Agent write: only Tickets assigned to that Agent, except self-assignment of an unassigned Ticket.
- Admin: every Ticket.
- Tickets assigned to another Agent return 404 to an Agent across details, Comments, status history, status changes, and assignment operations.

Assignment Contract
- Customer: no assignment operations.
- Agent: self-assign only when unassigned; unassign self only.
- Admin: assign, reassign, and unassign any non-Closed Ticket.
- Valid target: an existing UserRole.Agent only.
- Assignment does not change Ticket status.
- Closed Tickets reject assignment changes.

Idempotency and Concurrency
- Repeating the same assignment or deleting an empty assignment is a successful no-op.
- No-op requests do not update timestamps or create activity.
- Concurrent Agent claims allow one winner; the loser receives 409 without overwrite or activity.

Activity Data
Persist TicketId, ActorId, PreviousAssigneeId, NewAssigneeId, and CreatedAtUtc atomically with the Ticket change.

Dependencies
V21-D01, V21-D02, and V20-002.

Gherkin Scenarios
Agent self-assignment, hidden other-Agent Ticket, ownership-required Agent actions, Admin assignment/reassignment, invalid target role, Customer forbidden, Closed protection, idempotent repetition, concurrent claim, and unassignment.

Test Plan
Domain rule tests, API acceptance tests, row-level visibility regressions, and PostgreSQL atomicity/concurrency tests.

Definition of Done
Only approved Agent ownership changes persist; rejected operations preserve ownership; repeated no-op requests create no duplicate activity; existing Ticket endpoints enforce the approved row-level visibility contract.

Implementation Result
- Added PUT and DELETE assignee endpoints protected by the AgentOrAdmin policy.
- Added Domain assignment rules with idempotent bool results and Ticket concurrency-token renewal.
- Added row-level Agent read/write repository queries and existing-Agent target validation.
- Added atomic TicketAssignmentChange persistence and EF Core migration AddTicketAssignments.
- Protected assignment, status, and Comment writes with optimistic concurrency.
- Updated status and Comment tests to follow create -> assign -> write.
- Updated CONTEXT.md and README.md with ownership and visibility rules.

Validation
- dotnet build OpsDesk.sln --no-restore: 0 warnings, 0 errors.
- dotnet test OpsDesk.sln --no-build --no-restore: 120 passed, 0 failed, 0 skipped.
- dotnet format OpsDesk.sln --verify-no-changes --no-restore: passed.
- EF Core has-pending-model-changes: no pending model changes.
- Assignment integration group: 10 passed, including a real PostgreSQL concurrent-claim rollback test.
- Git commit and push were intentionally not performed; task is ready for user review.

#### Imported Comments

_No comments were stored._

#### Imported Subtasks

_No subtasks were stored._

### V21-004 - Expand the Ticket Activity Timeline

- Local status: `Done`
- Completed: `Yes`
- Original Asana section: `Done`
- Asana task ID: `1217550692979676`
- Created at: `2026-08-17T14:57:17.391Z`
- Modified at: `2026-08-27T13:11:13.271Z`
- Completed at: `2026-08-27T13:11:13.200Z`
- Historical Asana link: https://app.asana.com/1/1217520784789373/project/1217519861613556/task/1217550692979676

#### Original Description

Outcome
Present status, comment, and assignment activity as one coherent user-visible Ticket timeline.

Affected Layers
Domain, Application, Infrastructure, API, Tests

In Scope
Visible event types and response contract, deterministic query, actor and timestamp fields, visibility filtering, endpoint, and tests.

Out of Scope
SLA events, immutable admin audit log, notification events, and AI actions.

Public Seam
GET /tickets/{id}/activity or the approved Ticket-details contract.

Dependencies
V20-004, V21-001, V21-002, and V21-003.

Gherkin Scenarios
Generate scenarios showing mixed event types in stable chronological order and protected visibility.

Test Plan
API acceptance tests and PostgreSQL ordering and projection integration tests.

Definition of Done
All approved visible events share a stable contract and internal-only data remains hidden.

#### Imported Comments

_No comments were stored._

#### Imported Subtasks

_No subtasks were stored._

### V21-005 - Add Customer Resolution Review Flow

- Local status: `Done`
- Completed: `Yes`
- Original Asana section: `Done`
- Asana task ID: `1217793363951401`
- Created at: `2026-08-24T16:25:30.487Z`
- Modified at: `2026-08-28T15:12:07.674Z`
- Completed at: `2026-08-28T15:12:07.527Z`
- Historical Asana link: https://app.asana.com/1/1217520784789373/project/1217519861613556/task/1217793363951401

#### Original Description

Outcome
Require customer confirmation for final closure and let the requester reopen a resolved Ticket with an explicit reason.

Affected Layers
Domain, Application, Infrastructure, API, Tests

In Scope
Prevent Agent and Admin closure, requester-only reopen authorization, required reopen reason, atomic status change plus public comment and status-history persistence, endpoint, and tests.

Out of Scope
Formal appeals, escalations, automatic closure jobs, reopen time windows, follow-up Tickets, notifications, and SLA recalculation.

Public Seam
POST /tickets/{id}/reopen with a reason supplied by the requester. Existing requester-confirmed closure remains available through the approved status transition contract.

Dependencies
V20-003, V21-001, and V21-D01.

Gherkin Scenarios
Generate scenarios for requester reopen with a valid reason, support-user closure rejection, another Customer's hidden Ticket, invalid reason, and atomic persistence failure.

Test Plan
API acceptance tests, Application authorization tests, and PostgreSQL transaction tests.

Definition of Done
Support users cannot close a Ticket; the requester can atomically reopen their own resolved Ticket with a stored reason and status event; rejected operations preserve the previous state.

#### Imported Comments

_No comments were stored._

#### Imported Subtasks

_No subtasks were stored._

### V21-006 - Provision an Agent Account as Admin

- Local status: `Done`
- Completed: `Yes`
- Original Asana section: `Done`
- Asana task ID: `1217977680711851`
- Created at: `2026-08-30T22:42:02.811Z`
- Modified at: `2026-08-30T22:53:43.537Z`
- Completed at: `2026-08-30T22:53:43.410Z`
- Historical Asana link: https://app.asana.com/1/1217520784789373/project/1217519861613556/task/1217977680711851

#### Original Description

Outcome
Allow an authenticated Admin to provision an Agent account without direct database access.

Affected Layers
Application, API, Tests, Documentation

In Scope
Admin-only POST /admin/agents, server-owned Agent role, existing email and password validation, password hashing, duplicate-email protection, safe Agent response, and Agent login verification.

Out of Scope
Agent listing, pagination, invitations, email verification, password reset, corporate SSO, and direct database role changes.

Public Seam
POST /admin/agents

Request
First name, last name, email, and password. The request cannot select a role.

Response
201 Created with Agent ID, name, email, role, and creation time. Password and password hash are never returned.

Dependencies
V21-003 assignment workflow and existing authentication infrastructure.

Gherkin Scenarios
Given an authenticated Admin and valid Agent details, when the Admin creates an Agent, then the API returns 201 and the Agent can log in with an Agent role token.
Given a Customer or anonymous caller, when they call the endpoint, then the API returns 403 or 401.
Given an existing email, when an Admin creates the Agent, then the API returns 409.
Given invalid email or password input, when an Admin creates the Agent, then the API returns 400.

Validation Evidence - 2026-08-31
- Initial HTTP test failed with the expected 404 before implementation.
- Seven Agent provisioning HTTP cases passed.
- Full Release suite passed: 154/154 tests, 0 skipped.
- Release build passed with 0 warnings and 0 errors.
- Line coverage: 96.57% (3,240/3,355).
- Branch coverage: 80.18% (263/328).
- EF Core reported no pending model changes.
- Formatting, diff, and shortcut scans passed.

Pending
- Explicit Git stage, commit, and push approval.
- Manual Swagger acceptance before REL-210 release completion.

Definition of Done
All scenarios pass, no migration is required, documentation and release evidence are updated, and the change is reviewed before release.

#### Imported Comments

_No comments were stored._

#### Imported Subtasks

_No subtasks were stored._

### REL-210 - Validate and Release v2.1.0

- Local status: `Done`
- Completed: `Yes`
- Original Asana section: `Done`
- Asana task ID: `1217550832434470`
- Created at: `2026-08-17T14:57:17.373Z`
- Modified at: `2026-08-31T16:30:45.839Z`
- Completed at: `2026-08-31T16:30:45.733Z`
- Historical Asana link: https://app.asana.com/1/1217520784789373/project/1217519861613556/task/1217550832434470

#### Original Description

Outcome
Verify Ticket collaboration workflows before creating the v2.1.0 release.

Affected Layers
Domain, Application, Infrastructure, API, Tests, Documentation

In Scope
Full regression suite, PostgreSQL migrations, Admin Agent provisioning, comment and assignment Swagger flows, activity timeline, requester resolution review, README, release notes, and coverage evidence.

Out of Scope
Collection queries, SLA, and admin audit logs.

Public Seam
Public v2.1.0 API contract.

Dependencies
V21-001, V21-002, V21-003, V21-004, V21-005, and V21-006.

Gherkin Scenarios
All approved v2.1.0 task scenarios were rerun.

Validation Evidence - 2026-08-31
- dotnet format verification passed.
- Release build passed with 0 warnings and 0 errors.
- 154 of 154 tests passed; 0 skipped.
- Line coverage: 96.57% (3,240/3,355).
- Branch coverage: 80.18% (263/328).
- EF Core reported no pending model changes.
- Repository diff and shortcut scans found no release blocker.
- Manual Swagger acceptance covered Agent provisioning, login, and Ticket assignment.
- Agent provisioning feature commit: 4605b97.
- v2.1.0 release commit: 356c646.
- Annotated tag v2.0.0 points to 8693595.
- Annotated tag v2.1.0 points to 356c646.
- main and both tags were pushed to origin.

Definition of Done
Release evidence is recorded, the repository is clean, and the version tags are verified on origin.

#### Imported Comments

_No comments were stored._

#### Imported Subtasks

_No subtasks were stored._

### V22-D01 - Define the Ticket Collection Query Contract

- Local status: `Done`
- Completed: `Yes`
- Original Asana section: `Done`
- Asana task ID: `1217550692730036`
- Created at: `2026-08-17T14:57:17.367Z`
- Modified at: `2026-08-31T16:39:53.346Z`
- Completed at: `2026-08-31T16:39:45.694Z`
- Historical Asana link: https://app.asana.com/1/1217520784789373/project/1217519861613556/task/1217550692730036

#### Original Description

Outcome
Define and approve the Ticket collection query contract before implementation.

Affected Layers
API: accepts and validates query parameters.
Application: applies role-aware collection rules and returns paged results.
Infrastructure: executes the PostgreSQL query efficiently.
Tests: verifies public HTTP behavior and visibility boundaries.

Approved Public Seam
GET /tickets

All query parameters are optional. GET /tickets is therefore a valid request.

Approved Query Parameters
- page: integer, default 1, minimum 1.
- pageSize: integer, default 20, minimum 1, maximum 100.
- status: one optional TicketStatus value.
- priority: one optional TicketPriority value.
- requesterId: one optional user GUID.
- assigneeId: one optional Agent GUID.
- unassigned: optional boolean; true selects tickets with no assignee.
- sortBy: createdAtUtc, updatedAtUtc, or priority.
- sortDirection: asc or desc.

Approved Defaults and Ordering
- Default ordering is createdAtUtc descending, so newest tickets appear first.
- Ticket ID is the deterministic tie-breaker when the selected sort values are equal.
- Filters are optional and may be combined.
- assigneeId together with unassigned=true is an invalid combination.

Approved Response Shape
- items
- page
- pageSize
- totalCount
- totalPages
- hasPreviousPage
- hasNextPage

Approved Role Visibility
- Customer: only tickets requested by that customer.
- Agent: unassigned tickets and tickets assigned to that Agent.
- Admin: all tickets.
- Security visibility is applied before optional filters, counting, and pagination.
- totalCount reports only records visible to the authenticated caller.

Approved Error and Empty-Result Behavior
- Invalid query values, unsupported sort fields, invalid GUIDs, and conflicting filters return 400 Problem Details.
- A valid query with no matching tickets returns 200 with an empty items array and zero result metadata.
- Authentication and authorization continue to use the existing API behavior.

Out of Scope
Saved searches, full-text search, exports, dashboards, analytics, multiple values for one status or priority parameter, and cursor pagination.

Approved Gherkin Scenarios
1. Given a Customer has created tickets, when GET /tickets is requested, then only that Customer's tickets are returned.
2. Given an Agent has assigned and unassigned visible tickets, when GET /tickets is requested, then only those tickets are returned.
3. Given an Admin, when GET /tickets is requested, then all matching tickets are eligible for the result.
4. Given valid pagination values, when a page is requested, then stable items and correct metadata are returned.
5. Given valid filters, when they are combined, then filtering occurs inside the caller's visibility scope.
6. Given an invalid query value or conflicting assignment filters, when the endpoint is requested, then 400 Problem Details is returned.

Implementation Split
- V22-001 implements GET /tickets, role visibility, pagination, response metadata, and stable default ordering.
- V22-002 adds the approved filters and selectable sorting.

Definition of Done
Completed: every query parameter, default, limit, role rule, response field, and error behavior was explicitly approved on 2026-08-31.

#### Imported Comments

- **Hamza Çekirdek** at `2026-08-31T16:39:53.346Z`

  Hamza approved the complete GET /tickets query contract on 2026-08-31. The implementation is intentionally split: V22-001 covers role-aware pagination and stable default ordering; V22-002 adds filters and selectable sorting.

#### Imported Subtasks

_No subtasks were stored._

### V22-001 - List Tickets with Pagination

- Local status: `Done`
- Completed: `Yes`
- Original Asana section: `Done`
- Asana task ID: `1217550812817034`
- Created at: `2026-08-17T14:57:17.382Z`
- Modified at: `2026-09-01T14:00:53.373Z`
- Completed at: `2026-09-01T14:00:53.187Z`
- Historical Asana link: https://app.asana.com/1/1217520784789373/project/1217519861613556/task/1217550812817034

#### Original Description

Outcome
Return only the caller's visible Tickets with stable pagination metadata.

Affected Layers
Application, Infrastructure, API, Tests

In Scope
Paged response DTO, role-aware query, page limits, deterministic ordering, endpoint, efficient PostgreSQL projection, and tests.

Out of Scope
Filters beyond those needed for the base list, cursor pagination, full-text search, and exports.

Public Seam
GET /tickets?page={page}&pageSize={pageSize}.

Dependencies
V22-D01.

Gherkin Scenarios
Generate scenarios for a Customer's visible Tickets, staff visibility, empty results, and page boundaries.

Test Plan
API acceptance tests, PostgreSQL query integration tests, and visibility isolation tests.

Definition of Done
Pagination metadata and stable ordering are correct; page limits and role visibility are enforced.

#### Imported Comments

- **Hamza Çekirdek** at `2026-08-31T17:09:47.043Z`

  Approved design decision on 2026-08-31: GET /tickets will return TicketListItemResponse items rather than the full TicketResponse. Each list item will contain Id, Title, Priority, Status, RequesterId, AssigneeId, CreatedAtUtc, and UpdatedAtUtc. Description, ResolvedAtUtc, and ClosedAtUtc remain available from GET /tickets/{id}. This keeps collection payloads focused and avoids repeatedly transferring descriptions that can be up to 5,000 characters.
- **Hamza Çekirdek** at `2026-08-31T17:38:28.464Z`

  Implementation completed and ready for review on 2026-08-31. Added GET /tickets with default page=1 and pageSize=20, maximum pageSize=100, compact TicketListItemResponse items, deterministic createdAtUtc/Id ordering, pagination metadata, and role visibility: Customer sees own Tickets; Agent sees unassigned and self-assigned Tickets; Admin sees all Tickets. Queries use AsNoTracking, CountAsync, server-side visibility, Skip/Take, and SQL projection. No migration was added. Verification: format clean; build 0 warnings/0 errors; 165/165 tests passed with 0 skipped, including 11 new real-PostgreSQL HTTP cases.

#### Imported Subtasks

_No subtasks were stored._

### V22-002 - Filter and Sort Tickets

- Local status: `Done`
- Completed: `Yes`
- Original Asana section: `Done`
- Asana task ID: `1217550657108200`
- Created at: `2026-08-17T14:57:17.380Z`
- Modified at: `2026-09-02T22:16:06.719Z`
- Completed at: `2026-09-02T22:16:06.610Z`
- Historical Asana link: https://app.asana.com/1/1217520784789373/project/1217519861613556/task/1217550657108200

#### Original Description

Outcome
Apply the approved filters and sorting without breaking role visibility or pagination.

Affected Layers
Application, Infrastructure, API, Tests

In Scope
Approved status, priority, requester, and assignee filters; approved sort fields and directions; combined query behavior; validation and tests.

Out of Scope
Full-text search, saved filters, reporting, and database-specific search extensions.

Public Seam
GET /tickets with the query parameters approved in V22-D01.

Dependencies
V22-001.

Gherkin Scenarios
Generate scenarios for individual filters, combined filters, invalid values, and stable sorting across pages.

Test Plan
API acceptance tests, focused theory tests for query validation, and PostgreSQL query integration tests.

Definition of Done
Approved filters work alone and together, invalid values follow the contract, and pagination remains deterministic.

#### Imported Comments

- **Hamza Çekirdek** at `2026-09-01T14:33:08.058Z`

  Implementation is ready for review.

  - Added role-safe filtering by status, priority, requester, assignee, and unassigned state.
  - Added createdAtUtc, updatedAtUtc, and domain-priority sorting in asc/desc directions with deterministic ID tie-breakers.
  - Added validation for unsupported/malformed query values and conflicting assigneeId + unassigned=true filters.
  - Corrected Problem Details responses to use application/problem+json.
  - Added 24 PostgreSQL-backed integration scenarios for filters, combinations, role isolation, sorting, stable pagination, and validation.
  - No database migration was required.

  Validation: format clean; build 0 warnings / 0 errors; 189/189 tests passed.

#### Imported Subtasks

_No subtasks were stored._

### REL-220 - Validate and Release v2.2.0

- Local status: `Done`
- Completed: `Yes`
- Original Asana section: `Done`
- Asana task ID: `1217550693785305`
- Created at: `2026-08-17T14:57:17.380Z`
- Modified at: `2026-09-03T16:40:08.839Z`
- Completed at: `2026-09-03T16:40:08.810Z`
- Historical Asana link: https://app.asana.com/1/1217520784789373/project/1217519861613556/task/1217550693785305

#### Original Description

Outcome
Complete and verify the V2 Ticket Management release.

Affected Layers
Domain, Application, Infrastructure, API, Tests, Documentation

In Scope
Full tests, migration verification, collection-query Swagger examples, README, coverage artifact, release notes, secret and diff checks.

Out of Scope
V3 SLA, approvals, audit logs, background jobs, AI triage, and Git operations without separate approval.

Public Seam
Complete public V2 API contract.

Dependencies
V22-001 and V22-002.

Gherkin Scenarios
Re-run all approved V2 scenarios across v2.0.0, v2.1.0, and v2.2.0.

Test Plan
Full build, unit, integration, API acceptance, migration, Testcontainers, and coverage verification.

Definition of Done
All V2 behavior and documentation are verified; commit, push, and tag remain separately approved.

#### Imported Comments

_No comments were stored._

#### Imported Subtasks

_No subtasks were stored._

### AUTH-001 - Enforce Internet Email Address Format

- Local status: `Done`
- Completed: `Yes`
- Original Asana section: `Done`
- Asana task ID: `1217558732360427`
- Created at: `2026-08-17T17:54:15.180Z`
- Modified at: `2026-09-04T14:37:50.862Z`
- Completed at: `2026-09-04T14:37:50.735Z`
- Historical Asana link: https://app.asana.com/1/1217520784789373/project/1217519861613556/task/1217558732360427

#### Original Description

Outcome
Reject incomplete public email formats such as hamza@gmail while keeping self-registration available for development and portfolio use.

Affected Layers
Application, API, Tests, Documentation

In Scope
Strengthen EmailValidator rules, require a qualified domain name, preserve normalization, return a clear 400 response, and add unit/API regression tests.

Out of Scope
DNS/MX lookup, mailbox ownership checks, sending email, invitations, and SSO.

Acceptance Criteria
- hamza@gmail is rejected.
- hamza@gmail.com is accepted.
- Blank, whitespace-containing, and malformed addresses remain rejected.
- Rejected registration does not create a User.
- Existing admin seed validation remains compatible with admin@opsdesk.local.

BDD Scenarios

Scenario: Reject an unqualified email during registration
Given public registration is available
When a User registers with hamza@gmail
Then the API returns 400 Problem Details
And no User is persisted

Scenario: Accept a qualified email during registration
Given public registration is available
When a User registers with hamza@gmail.com
Then registration succeeds

Scenario: Preserve the development admin seed
Given the development admin seed uses admin@opsdesk.local
When the application starts
Then the seed email remains valid

Implementation Notes
- Public behavior seam: POST /auth/register.
- Unit seam: IEmailValidator.Validate.
- A qualified domain contains at least two non-empty labels.
- Validation runs before repository persistence.

#### Imported Comments

_No comments were stored._

#### Imported Subtasks

_No subtasks were stored._
