# Mini Order Management System

ASP.NET Core (.NET 8) Web API + MySQL, with plain HTML/JS pages served as static files
(no frontend framework, no CSS framework).

## Stack

- C# / ASP.NET Core 8 Web API
- MySQL 8, accessed via EF Core + the Pomelo MySQL provider
- Plain HTML + vanilla JavaScript (`fetch`) for the UI, served from `wwwroot/`
- xUnit for automated tests

## Project structure

```
Database/
  schema.sql               -- standalone DDL + sample data (Part A)
OrderManagement.Api/
  Controllers/              -- thin HTTP layer (ProductsController, OrdersController)
  Services/OrderService.cs  -- order creation + history business logic (unit-testable)
  Data/AppDbContext.cs      -- EF Core DbContext
  Models/                   -- Product, Order, OrderItem
  DTOs/                     -- request/response shapes + data-annotation validation
  Migrations/               -- EF Core migrations (mirrors schema.sql)
  wwwroot/
    index.html               -- Part B: product list
    order.html                -- Part C + D: add items to an order, then submit it
    history.html               -- Part E: order history, newest first
OrderManagement.Tests/
  OrderServiceTests.cs      -- automated tests (see "Testing" below)
```

## How to run

### 1. Database

Create the database and load the schema + sample products:

```sql
CREATE DATABASE order_management CHARACTER SET utf8mb4;
```

```
mysql -u <user> -p order_management < Database/schema.sql
```

`schema.sql` creates `Products`, `Orders`, `OrderItems` and inserts 6 sample products.
This script is the source of truth for anyone setting up the DB by hand; it's kept in
sync with the EF Core migration under `OrderManagement.Api/Migrations/`, which is what
the app itself uses if you'd rather run `dotnet ef database update` instead.

### 2. Connection string

The app reads `ConnectionStrings:DefaultConnection` from configuration. Don't put
credentials in `appsettings.json` — use user-secrets for local dev:

```
cd OrderManagement.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=order_management;User=<user>;Password=<password>;"
```

### 3. Run the app

```
cd OrderManagement.Api
dotnet run
```

Open `http://localhost:5014/` (or whatever port the console prints). The pages link to
each other:

- `/index.html` — product list
- `/order.html` — add products to an order and submit it
- `/history.html` — submitted orders, newest first

Swagger is available at `/swagger` in Development.

### 4. Run the tests

```
dotnet test
```

## Database design (Part A)

Three tables, matching the requirement exactly:

- **Products** — `Id, Name, Sku (unique), Description, Price, StockQuantity`
- **Orders** — `Id, OrderDate`
- **OrderItems** — `Id, OrderId (FK), ProductId (FK), Quantity, UnitPrice`

`OrderItems.UnitPrice` is a copy of the product's price _at the time of the order_,
not a live join to `Products.Price` — so historical orders stay accurate even if a
product's price changes later. `OrderId` cascades on delete (deleting an order deletes
its items); `ProductId` is restricted (a product referenced by an order can't be
deleted out from under it).

## Validation (Part C)

Client-side (in `order.html`, before the request is even sent):

- a product must be selected
- quantity must be a positive whole number (`Number.isInteger(quantity) && quantity > 0`)
- quantity (plus whatever of that product is already in the cart) can't exceed the
  product's `stockQuantity`

Server-side (defense in depth — the client-side checks above are a convenience, not a
security boundary):

- `CreateOrderItemDto.Quantity` has a `[Range(1, int.MaxValue)]` data annotation, checked
  via `ModelState` before the request reaches business logic
- `OrderService.CreateOrderAsync` re-checks quantity > 0, checks that every submitted
  `ProductId` actually exists in the database, and checks each product's _total_ requested
  quantity (summed across items, in case the same product appears twice) against its
  current `StockQuantity` — all things no data annotation can express, since they need a
  DB lookup. Each returns a clear, specific error message.

Note this is a **read-only check**, not inventory management: we compare the requested
quantity against the current `StockQuantity`, but never decrement it — see "Assumptions"
below for why.

## Parameterized queries

All database access goes through EF Core (`DbContext` LINQ queries and `SaveChangesAsync`),
which parameterizes every generated SQL statement — there is no string-concatenated SQL
anywhere in the codebase. `Database/schema.sql` is a static DDL/seed script (no user input
involved), not an app query path.

## Decisions: empty orders & duplicate submissions (Part D)

**Empty order:** rejected outright, both client-side (submit button is disabled while the
cart is empty) and server-side (`OrderService.CreateOrderAsync` returns a 400 if `Items` is
empty). An order with nothing in it has no meaning to persist.

**Duplicate submission:** handled at the UI level only — the submit button is disabled the
instant it's clicked, so a double-click or a slow response can't fire a second request.
I deliberately did _not_ add server-side de-duplication (e.g. an idempotency key, or
rejecting an order that looks identical to one just submitted). Reasoning: there's no
login/session in this system, so there's no reliable way to tell "the same order submitted
twice by accident" apart from "the user genuinely wants to place two identical orders back
to back" (e.g. two separate customers at a counter, or a deliberate re-order). Blocking on
similarity would risk silently dropping a legitimate order. Given the scope (no auth, no
deployment), UI-level prevention is the simplest thing that actually addresses the realistic
failure mode (accidental double-click), without introducing false positives.

## Testing (Part E requirement, listed under Part D in the spec)

Six tests in `OrderManagement.Tests/OrderServiceTests.cs`, all against `OrderService`
using EF Core's InMemory provider (fast, no MySQL dependency for CI):

1. `CreateOrderAsync_RejectsEmptyOrder` — an order with no items is rejected and nothing is written.
2. `CreateOrderAsync_RejectsNonPositiveQuantity` — a zero quantity is rejected.
3. `CreateOrderAsync_RejectsQuantityExceedingStock` — a quantity above the product's current stock is rejected, and stock itself stays unchanged (checked, not decremented).
4. `CreateOrderAsync_RejectsNonExistentProduct` — a product ID that doesn't exist is rejected, with a useful error message.
5. `CreateOrderAsync_PersistsOrderWithCorrectTotal_WhenValid` — the happy path: a valid multi-item order is saved, the computed total is correct, and stock is confirmed untouched.
6. `GetOrderHistoryAsync_ReturnsNewestFirst` — history ordering is correct.

**Why these:** the evaluation criteria rank "the core flow works" and "validation on both
sides" above test coverage breadth, so I focused tests on `OrderService` — the one piece of
business logic where a bug would silently corrupt data (wrong total, an order for a
nonexistent product, or an empty order landing in the database). I didn't write
controller/HTTP-level tests or tests for the read-only `GetAll` product listing, since
that's a straight passthrough with no logic to break — testing it would mostly be testing
EF Core itself.

## Assumptions

- No authentication: any visitor can place an order and view history, per the spec ("no
  login/authentication").
- No stock _management_: placing an order does **not** decrement `StockQuantity` — there's
  no inventory tracking over time, per the spec ("no stock management"). An earlier
  version of this project did implement stock deduction; it was intentionally removed to
  match the final requirements. `StockQuantity` is validated, though: an order is rejected
  if it requests more than the product's current stock. I read "no stock management" as
  "don't build inventory tracking/deduction," not "ignore the stock number that's already
  on the product" — refusing to place an order for more than exists on record is a basic
  sanity check, not inventory management.
- A single global order history (no per-customer view), since there's no concept of a
  logged-in customer.
- Order quantities are per-product; adding the same product to an order twice in the UI
  merges into one line with a summed quantity, rather than creating two `OrderItems` rows
  for the same product.
