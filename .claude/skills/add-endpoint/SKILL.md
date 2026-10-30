---
name: add-endpoint
description: Add or change an HTTP endpoint end to end (DTOs, service method, controller action, tests) in the fixed solution layout. Use for any new API action or a change to an existing one.
---

# Add an endpoint

Follow these steps in order. The file list in step 1 is the whole change: if the work needs a file that isn't on it, stop and say why before creating it.

## 1. Say what will change

Before writing code, list:

- the route and HTTP method;
- request and response DTOs (new or existing);
- the service method (existing service, or new one in an existing feature folder);
- whether a repository method is needed (only for an aggregate load `GetById*` or a query a second caller needs);
- the test seam (from the plan, or ask).

Expected files, nothing else:

| What | Where |
|------|-------|
| Request/response DTOs | `AppName.Shared/Types/<Feature>/<Name>Dto.cs`, one record per file |
| Service method | `AppName.Core/Modules/<Feature>/<Feature>Service.cs` |
| Business rule, if there is a real one | static method in `<Feature>Rules.cs` or on the entity |
| Repository read, if needed | `AppName.Core/Modules/<Feature>/<Aggregate>Repository.cs` |
| Controller action | `AppName.Web/Controllers/<Feature>Controller.cs` |
| Tests | `AppName.Tests/<Feature>/` |

Reuse an existing DTO when it already has the right shape. Don't create a second set of API models.

## 2. Test first

Write the failing test at the agreed seam:

- business rule → plain unit test of the static method, no mocks;
- service → real `DbContext` (SQLite in-memory or Testcontainers), repository mocked only to control what gets loaded;
- API contract → a `WebApplicationFactory` test, only if the plan asked for one.

Expected values come from the request or a worked example.

## 3. Implement

- Service method: validation inline at the top with early returns; load what is needed in a few queries before any loop; writes through `DbContext` then `SaveChangesAsync`. Throw the repo's exception types for not-found and rule violations.
- Controller action: one service call, return the result. No try/catch, no `DbContext`, no mapping beyond a line or two.
- Mapping between entity and DTO: a few lines in the service or a static method. No mapping class.

## 4. Finish

Run the "Before you finish" check from the coding guideline. In the summary, list the new types with one line each saying why they exist, and any existing endpoint whose contract changed.
