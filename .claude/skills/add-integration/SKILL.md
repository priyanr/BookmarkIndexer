---
name: add-integration
description: Connect to a new external system (third-party API, mail provider, payment gateway, regulator or partner endpoint) or add calls to an existing one. Use whenever code needs to talk to something outside this solution.
---

# Add an integration

One external system = one boundary interface in Shared + one implementation folder in Integrations. Nothing else talks to the outside world.

## 1. Check what exists

- Is there already a boundary interface for this system in `AppName.Shared`? Add methods to it instead of creating a second one.
- Is there an integration with the same shape (auth, retries, error mapping) in `AppName.Integrations`? Match it.
- Get a real sample request and response (from the provider's docs or a captured call). Ask for one if you don't have it. Don't invent payloads.

## 2. Files

| What | Where |
|------|-------|
| Boundary interface (one per external system) | `AppName.Shared/I<System>Client.cs` |
| DTOs the rest of the app sees | `AppName.Shared/Types/<System>/` |
| Implementation | `AppName.Integrations/<System>/<System>Client.cs` |
| The provider's JSON shapes | private nested records inside the client, not separate files |
| Options (base URL, credentials) | one options record next to the client, bound with `IOptions<T>` |
| DI registration | the existing registration method (`AddCore` or `Program.cs`) |
| Tests | `AppName.Tests/Integrations/<System>/` |

The interface speaks the app's language (`SubmitClaim(ClaimSubmissionDto)`), not the provider's (`PostV2EClaimRequest`). Translating between the two is the client's job.

## 3. Test first

- Client: a fake `HttpMessageHandler` that returns the sample response. Assert the request the client sends (URL, headers, body) and the DTO it returns, including the provider's error responses.
- Code that uses the client: fake the boundary interface.
- No interface around `HttpClient`. Use a typed client.

## 4. Implement

- Use `HttpClient` through a typed client, `ILogger<T>`, `IOptions<T>` and `TimeProvider` directly. No wrappers, no base client class.
- No retries, caching or queues unless the plan names the need. If the provider documents rate limits or retry rules, say so and ask.
- Credentials come from configuration, never from code or test files.
- Don't log request or response bodies that contain personal or medical data. Log the correlation id, status code and timing.
- Map provider errors to the repo's exception types with a message that says what the provider returned.

## 5. Finish

Run the "Before you finish" check from the coding guideline. In the summary, list the configuration keys that need values in each environment.
