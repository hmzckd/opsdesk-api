# OpsDesk Support Context

OpsDesk coordinates internal support requests between people requesting help and support staff responsible for resolving those requests.

## Language

**User**:
An authenticated person who can interact with OpsDesk. A User has exactly one role: Customer, Agent, or Admin.
_Avoid_: Account, member

**Customer**:
A User who requests support and follows the progress of their Tickets.
_Avoid_: Client, end user

**Agent**:
A support User who investigates and resolves unassigned Tickets they claim or Tickets currently assigned to them.
_Avoid_: Operator, technician

**Admin**:
A privileged User who manages protected operational actions across OpsDesk.
_Avoid_: Superuser

**Ticket**:
A recorded request for support or operational assistance. A Ticket keeps its requester for its entire lifetime.
_Avoid_: Issue, case, request

**Requester**:
The User on whose behalf a Ticket is created. In V2, the requester is the authenticated creator.
_Avoid_: Owner, creator, customer ID

**Assignee**:
The Agent currently responsible for handling a Ticket. A Ticket can be unassigned.
_Avoid_: Owner, handler

**Ticket Assignment Change**:
A permanent record that identifies who assigned or unassigned a Ticket, the previous Assignee, the new Assignee, and when the change happened.
_Avoid_: Status change, comment

**Unassigned Ticket**:
A Ticket that does not currently have an Assignee. Agents can discover and claim it, but must claim it before changing status or adding a Comment.
_Avoid_: Unowned Ticket

**Ticket Status**:
The current lifecycle position of a Ticket.

**Open**:
The Ticket has been received but is not currently being worked on.

**In Progress**:
An Agent is actively working on the Ticket.

**Waiting for Customer**:
Work cannot continue until the requester provides information or responds.

**Resolved**:
A solution has been provided and is waiting for requester confirmation.

**Closed**:
The requester has accepted the resolution. The Ticket is final and no longer accepts active handling.

**Reopen**:
The requester rejects a proposed resolution, supplies a required public reason, and returns the Ticket to In Progress. Support Users cannot reopen a resolved Ticket on the requester's behalf.
_Avoid_: Appeal, dispute

**Ticket Comment**:
A public message added to a Ticket conversation by an authenticated User. A Ticket Comment keeps its author and creation time permanently.
_Avoid_: Internal note, audit entry

**Ticket Activity Timeline**:
A chronological read view that combines existing Ticket Comments, Ticket Status Changes, system-generated SLA Breaches, and role-appropriate Ticket Assignment Changes. It is not a separate permanent record. Customers see public activity only; Agents and Admins can also see internal assignment activity. A system-generated item has no User actor.
_Avoid_: Audit log, activity table

**Audit Log**:
An Admin-only record of who made a significant operational change, what was affected, and when it happened. It records successful database changes rather than claiming that an external email was delivered. It is separate from the Ticket Activity Timeline and never contains passwords or tokens.
_Avoid_: Ticket activity, email delivery receipt

**Ticket Priority**:
The operational urgency assigned to a Ticket. V2 supports Low, Medium, High, and Urgent priority.
_Avoid_: Severity, importance

**SLA Policy**:
The active resolution duration OpsDesk applies to a new Ticket for one Ticket Priority. Changing a policy does not change deadlines already assigned to existing Tickets.
_Avoid_: Deadline, timeout

**SLA Deadline**:
The immutable UTC time by which a Ticket is expected to be resolved. It starts from the Ticket's creation time and continues while the Ticket is Waiting for Customer.
_Avoid_: Expiration time, due date

**SLA Breach**:
The condition in which a Ticket remains unresolved after its SLA Deadline or was resolved after that deadline. Reopening a Ticket keeps the original deadline. The current state is calculated from the Ticket snapshot, while the first detected breach is stored once as a durable system event. PostgreSQL performs the bounded insert atomically, enforces one event per Ticket, and checks that detection happened after the stored deadline. This database-owned creation path prevents duplicate events across concurrent workers, so no unused Domain factory is exposed for creating breach records.
_Avoid_: Ticket expiration, automatic escalation

**Ticket Status History**:
The user-visible sequence of status changes made during a Ticket's lifetime.
_Avoid_: Audit log

**Ticket Status Change**:
A recorded movement from one Ticket Status to another, made by an authenticated User at a specific time.
_Avoid_: Status update, audit entry
