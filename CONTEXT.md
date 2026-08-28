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
A chronological read view that combines existing Ticket Comments, Ticket Status Changes, and role-appropriate Ticket Assignment Changes. It is not a separate permanent record. Customers see public activity only; Agents and Admins can also see internal assignment activity.
_Avoid_: Audit log, activity table

**Ticket Priority**:
The operational urgency assigned to a Ticket. V2 supports Low, Medium, High, and Urgent priority.
_Avoid_: Severity, importance

**Ticket Status History**:
The user-visible sequence of status changes made during a Ticket's lifetime.
_Avoid_: Audit log

**Ticket Status Change**:
A recorded movement from one Ticket Status to another, made by an authenticated User at a specific time.
_Avoid_: Status update, audit entry
