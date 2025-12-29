# Critical Service API Template

Small ASP.NET Core Web API created as a learning project to explore backend fundamentals commonly encountered in production systems.

The goal of this repository is to practice and understand reliability-oriented patterns in a minimal and isolated setup.  
This is **not** a complete product, but a focused technical exercise.

---

## Why this project exists

When working on backend systems, some concerns appear repeatedly:
- handling retries safely
- avoiding duplicate writes
- preventing lost updates
- making failures observable

This project is a small attempt to experiment with these concerns at the API level, using a simple domain.

---

## What this project covers

- Idempotent POST requests using an `Idempotency-Key` header
- Optimistic concurrency with `ETag` / `If-Match`
- Centralized error handling using ProblemDetails
- Request correlation via an `X-Correlation-Id` header
- Entity Framework Core with migrations
- Dockerized API for reproducible execution

Each topic is intentionally kept simple and isolated.

---

## Tech stack

- .NET 8 (LTS)
- ASP.NET Core (Minimal APIs)
- Entity Framework Core
- SQLite (for simplicity)
- Docker

---

## Running locally (without Docker)

```bash
dotnet run
````

The API will be available on the HTTPS port displayed in the console.

---

## Running with Docker

```bash
docker build -t critical-service-api .
docker run -p 8080:8080 critical-service-api
```

The API will be available at:

```
http://localhost:8080
```

---

## Example requests (PowerShell)

> In Windows PowerShell, use `curl.exe` (since `curl` can be an alias of `Invoke-WebRequest`).

### Create a resource

```powershell
curl.exe -X POST "http://localhost:8080/todos?title=FirstTodo"
```

---

### Idempotent creation

Submitting the same request twice with the same idempotency key will not create duplicates.

```powershell
curl.exe -X POST "http://localhost:8080/todos?title=IdempotentTodo" `
  -H "Idempotency-Key: demo-1"

curl.exe -X POST "http://localhost:8080/todos?title=IdempotentTodo" `
  -H "Idempotency-Key: demo-1"
```

---

### Optimistic concurrency

```powershell
# Retrieve the resource and its ETag
curl.exe -i "http://localhost:8080/todos/{id}"

# Update using If-Match
curl.exe -i -X PUT "http://localhost:8080/todos/{id}?title=Updated" `
  -H 'If-Match: "1"'
```

If the version does not match, the API returns:

```
412 Precondition Failed
```

---

## Notes

* SQLite is used for demo and learning purposes only
* HTTPS termination is expected to be handled by the hosting platform. If you run it with debug in visual studio you may need to use HTTPS instead.
* This project is intentionally small and focused on backend fundamentals

## Live demo

https://hussein-elamine.vercel.app/projects/critical-service

or

https://criticalservicetemplate.fly.dev/todos
