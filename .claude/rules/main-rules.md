# Architecture Decision Record

## Coding conventions

- Use "Async" suffix in names of methods that return an awaitable type

## Forbidden patterns

- **Prohibition of the Repository Pattern** — Do not implement the Repository pattern in Entity Framework projects. To reduce complexity and prevent anti-patterns, use DbContext directly across the codebase to simplify data access and improve maintainability.
- **No CQRS** — do not implement Command Query Responsibility Segregation.
- **No Mediator** — do not use MediatR or similar libraries. Use direct service injection.
- Legacy `CommandHandlers/`, `QueryHandlers/`, `Commands/`, `Queries/` directories exist but must not be extended.
- Do not call validators manually — rely on the FluentValidation pipeline middleware to validate automatically.
- AutoMapper is not allowed to be used. In case you touch an existing code, migrate it.
- When validating a collection of items against the database or fetching related data for a list of entities, do not use any pattern that calls a DB method inside a loop — with or without `Task.WhenAll`.

## Data Access

- **Inject `SqlContext` directly** into controllers and service classes. Do not go through a repository layer for new code.
- **Never create new repository classes or interfaces.**
- **Always add `.AsNoTracking()`** on read-only queries. Omitting it is a performance bug — EF Core will track every loaded entity unnecessarily.
- **Add `.AsSplitQuery()`** when a query loads multiple collection `.Include()` chains to avoid the Cartesian explosion problem.
- **Use `ExecuteDeleteAsync()` / `ExecuteUpdateAsync()`** for bulk operations without loading entities into memory first.
- When validating or fetching data for a collection of items, always use a single batched query instead of calling the database once per item.
