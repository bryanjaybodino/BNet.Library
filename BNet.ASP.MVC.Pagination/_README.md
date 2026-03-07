# BNet.ASP.MVC.Pagination

A lightweight ASP.NET Core MVC helper that adds **server-side pagination** and **live search** to any `List<T>` — no full page reloads, no extra dependencies.

---

## Table of Contents

- [How It Works](#how-it-works)
- [Requirements](#requirements)
- [Installation](#installation)
- [Quick Start](#quick-start)
  - [1. Register Session Middleware](#1-register-session-middleware)
  - [2. Set Up Your Model](#2-set-up-your-model)
  - [3. Set Up Your Service / Repository](#3-set-up-your-service--repository)
  - [4. Set Up Your Controller](#4-set-up-your-controller)
  - [5. Set Up Your View](#5-set-up-your-view)
- [API Reference](#api-reference)
  - [GridView Properties](#gridview-properties)
  - [SetGridView()](#setgridview)
  - [NewPagination()](#newpagination)
  - [PaginationChangedEventArgs](#paginationchangedeventargs)
- [Search Integration](#search-integration)
- [CSS Customisation](#css-customisation)
- [Notes & Gotchas](#notes--gotchas)
- [Breaking Changes & Migration](#breaking-changes--migration)
- [Resources](#resources)
---

## How It Works

```
Browser                          Server
  │                                │
  │──── GET /Home ────────────────►│  IndexAsync() calls SetGridView()
  │◄─── Full page (+ injected JS)──│
  │                                │
  │  [User clicks page 2]          │
  │──── POST /Home/Pagination ────►│  GridView_PaginationChanged() calls NewPagination()
  │◄─── PartialView (HTML only) ───│
  │                                │
  │  JS replaces <div id="...">    │
```

`GridView` injects a small `fetch`-based script into the rendered HTML. When a page button is clicked, it POSTs to your pagination route and swaps only the table `<div>` — no full page reload.

---

## Requirements

- .NET 6 or later
- ASP.NET Core MVC
- Session middleware enabled (see below)

---

## Installation

Copy `GridView.cs` into your project (e.g. under a `Pagination/` folder) and add the namespace to your using statements:

```csharp
using BNet.ASP.MVC.Pagination;
```

---

## Quick Start

### 1. Register Session Middleware

`GridView` uses `ISession` to persist row size, page size, and sequence across requests. Your full `Program.cs` should look like this:

```csharp
using BNet.ASP.MVC.Pagination.Sample.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<MyDBContext>();
builder.Services.AddScoped<IUsersServices, UsersServices>();
builder.Services.AddSession();  // SESSION IS REQUIRED TO ADD SEARCH FILTER

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseSession(); // SESSION IS REQUIRED TO ADD SEARCH FILTER

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
```

> `AddSession()` is registered in the service collection, and `UseSession()` is called in the middleware pipeline before routing.

---

### 2. Set Up Your Model

```csharp
using System.ComponentModel.DataAnnotations;

namespace BNet.ASP.MVC.Pagination.Sample.Models
{
    public class users_table
    {
        [Key]
        public int DBID { get; set; }
        public string? DBFirstName   { get; set; }
        public string? DBLastName    { get; set; }
        public string? DBAge         { get; set; }
        public string? DBDateCreated { get; set; }

        // DTO used for the GridView list
        public class dataDTO
        {
            public int     id          { get; set; }
            public string? firstname   { get; set; }
            public string? lastname    { get; set; }
            public string? age         { get; set; }
            public string? datecreated { get; set; }
        }
    }
}
```

---

### 3. Set Up Your Service / Repository

**Interface:**

```csharp
public interface IUsersServices
{
    Task<List<users_table.dataDTO>> GetAllAsync(string search);
}
```

**Implementation:**

```csharp
public class UsersServices : IUsersServices
{
    private readonly MyDBContext context;

    public UsersServices(MyDBContext context) => this.context = context;

    public async Task<List<users_table.dataDTO>> GetAllAsync(string search)
    {
        var users = string.IsNullOrEmpty(search)
            ? await context.users_table.ToListAsync()
            : await context.users_table
                .Where(x => x.DBFirstName.Contains(search) ||
                            x.DBLastName.Contains(search))
                .ToListAsync();

        return users.Select(u => new users_table.dataDTO
        {
            id          = u.DBID,
            firstname   = u.DBFirstName,
            lastname    = u.DBLastName,
            age         = u.DBAge,
            datecreated = u.DBDateCreated,
        }).ToList();
    }
}
```

**DbContext:**

```csharp
public class MyDBContext : DbContext
{
    public MyDBContext(DbContextOptions<MyDBContext> options) : base(options) { }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        string conn = $"Data Source=(LocalDB)\\MSSQLLocalDB;" +
                      $"AttachDbFilename={Environment.CurrentDirectory}\\AccountDB.mdf;" +
                      $"Integrated Security=True;Connect Timeout=30";
        optionsBuilder.UseSqlServer(conn);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<users_table>(e =>
            e.HasKey(x => x.DBID).HasName("users_table_entity"));
    }

    public DbSet<users_table> users_table { get; set; }
}
```

Register in `Program.cs`:

```csharp
builder.Services.AddDbContext<MyDBContext>();
builder.Services.AddScoped<IUsersServices, UsersServices>();
```

---

### 4. Set Up Your Controller

```csharp
using BNet.ASP.MVC.Pagination;
using BNet.ASP.MVC.Pagination.Sample.Models;
using BNet.ASP.MVC.Pagination.Sample.Repositories;
using Microsoft.AspNetCore.Mvc;
using static BNet.ASP.MVC.Pagination.GridView;

public class HomeController : Controller
{
    private readonly IUsersServices usersServices;
    GridView gridView = new GridView();

    public HomeController(IUsersServices usersServices)
        => this.usersServices = usersServices;

    // ── GET /Home ──────────────────────────────────────────────────────────
    public async Task<IActionResult> IndexAsync(string? search, string? test)
    {
        search = search ?? string.Empty;

        // Persist search term so it survives pagination POST requests
        HttpContext.Session.SetString("Search", search);

        List<users_table.dataDTO> users = await usersServices.GetAllAsync(search);

        gridView.FirstAndLast = true; // show First / Last jump buttons (optional)

        gridView.SetGridView(
            context   : HttpContext,
            dataList  : users,
            routeName : "Home/Pagination", // must match the [Route] below
            tableName : "htmlTableUsers",  // must match the id on your wrapper element
            rowSize   : 8,                 // rows per page
            pageSize  : 5                  // numbered buttons visible at once
        );

        gridView.PaginationChanged += GridView_PaginationChanged;
        return View("~/Views/Home/Index.cshtml", gridView);
    }

    // ── POST /Home/Pagination ──────────────────────────────────────────────
    // Route name here MUST match the routeName passed to SetGridView()
    [Route("Home/Pagination")]
    [HttpPost]
    public async Task<IActionResult> GridView_PaginationChanged(PaginationChangedEventArgs e)
    {
        // Retrieve the persisted search term so filtering is maintained across pages
        string search = HttpContext.Session.GetString("Search") ?? string.Empty;

        List<users_table.dataDTO> users = await usersServices.GetAllAsync(search);

        gridView.NewPagination(HttpContext, users, e);

        // Return a PartialView — the JS will extract only the table div from the HTML
        return PartialView("~/Views/Home/Index.cshtml", gridView);
    }
}
```

> **Important:** The `routeName` in `SetGridView()` and the `[Route("...")]` attribute on the POST action must be identical.

---

### 5. Set Up Your View

The view uses `@model BNet.ASP.MVC.Pagination.GridView` and has three responsibilities: render the table rows, render `page_entry` + `pagination`, and call the auto-generated search function.

```razor
@* Views/Home/Index.cshtml *@
@model BNet.ASP.MVC.Pagination.GridView

<div>
    <h4>ASP.Net MVC Pagination</h4>
    <hr />
</div>
<div class="row">
    <div class="col-lg-4">
        <b>Search</b>
        <input id="Search" onkeyup="SearchEvent()" class="form-control form-control-sm" />
    </div>
</div>
<div class="table-responsive pt-4">
    <table class="table table-sm" id="htmlTableUsers">
        <thead>
            <tr>
                <th>Id</th>
                <th>FirstName</th>
                <th>LastName</th>
                <th>Age</th>
                <th>DateCreated</th>
            </tr>
        </thead>
        <tbody>
            @{
                try // YOU MUST USE TRY CATCH HERE
                {
                    for (int i = Model.start; i < Model.end; i++)
                    {
                        <tr>
                            <td>@Model.table[i].id</td>
                            <td>@Model.table[i].firstname</td>
                            <td>@Model.table[i].lastname</td>
                            <td>@Model.table[i].age</td>
                            <td>@Model.table[i].datecreated</td>
                        </tr>
                    }
                }
                catch { }
                // THE PAGE ENTRY AND PAGINATION MUST ALWAYS BE BELOW OR ABOVE THE TRY CATCH
                <tr>
                    <td colspan="5">
                        <div>
                            @Model.page_entry
                        </div>
                        <div>
                            @Model.pagination
                        </div>
                    </td>
                </tr>
            }
        </tbody>
    </table>
</div>
<script>
    //// VERSION 1.0.1
    function SearchEvent()
    {
        var val = document.getElementById('Search').value;
        htmlTableUsers_SearchEvent({
            search: val,            // maps to IndexAsync(string? search, ...)
            test: "Add more filter" // maps to IndexAsync(..., string? test)
        });
    }
</script>
```

**Three things to remember in the view:**

1. **`try/catch` around the row loop** — `Model.Start` and `Model.End` may be momentarily out of range on first load; the catch silently skips rendering rather than throwing.
2. **`page_entry` and `pagination` outside the `try/catch`** — these must always render regardless of whether the row loop succeeded.
3. **`id="htmlTableUsers"` on the `<table>`** — this must exactly match the `tableName` you passed to `SetGridView()`. The injected JS uses this id to find and swap the element content on pagination clicks.

> **v1.0.1 note:** `Model.Table` is typed as `object` — you cannot index it directly with `[i]`. Cast it to your DTO list first at the top of the `@{ }` block:
> ```razor
> var table = Model.Table as List<users_table.dataDTO>;
> ```
> Then use `table[i].firstname` etc. in the loop. See [Breaking Changes & Migration](#breaking-changes--migration) for the full updated example.

**How the search function works:**

`GridView` auto-generates a JS function named `{tableName}_SearchEvent` (in this case `htmlTableUsers_SearchEvent`) and injects it into the page via `@Model.pagination`. You call it from your own `SearchEvent()` with an object whose **keys match your controller action's parameter names**:

```javascript
htmlTableUsers_SearchEvent({
    search: val,           // → IndexAsync(string? search, ...)
    test: "Add more filter" // → IndexAsync(..., string? test)
});
```

This fires a `GET` to your controller with those values as query parameters, then swaps the table content with the response — no full page reload.

---

## API Reference

### GridView Properties

| Property | Type | Default | Description |
|---|---|---|---|
| `FirstAndLast` | `bool` | `false` | Show jump-to-first and jump-to-last buttons |
| `CSS_Pagination` | `string` | `"pagination"` | CSS class on the pagination wrapper `<div>` |
| `CSS_Button` | `string` | `"page-item page-link btn rounded-0"` | CSS classes on each page button |
| `CSS_PageIndex` | `string` | `"bg-primary text-white"` | Extra classes applied to the **active** page button |
| `start` | `int` | — | Start index into `table` for the current page (read-only output) |
| `end` | `int` | — | End index into `table` for the current page (read-only output) |
| `table` | `object` | — | The full list cast to `object`; index with `Model.table[i]` in the view |
| `pagination` | `HtmlString` | — | Rendered pagination controls + injected JS |
| `page_entry` | `HtmlString` | — | "Showing X to Y of Z entries" label |

---

### SetGridView()

Called on the **initial GET** request.

```csharp
gridView.SetGridView(
    HttpContext context,
    List<T>     dataList,
    string      routeName,   // route of the POST pagination action
    string      tableName,   // id of the HTML wrapper element
    int         rowSize,     // number of data rows per page
    int         pageSize     // number of page buttons shown at once
);
```

---

### NewPagination()

Called inside the **POST pagination action**. Reads `rowSize` and `pageSize` from session automatically — no need to pass them again.

```csharp
gridView.NewPagination(
    HttpContext                  context,
    List<T>                      dataTable,
    PaginationChangedEventArgs   e
);
```

---

### PaginationChangedEventArgs

Automatically bound from the query string by the POST action.

| Property | Type | Description |
|---|---|---|
| `NewPageIndex` | `string` | Page to navigate to (`"0"`–`"N"`, `"NEXT"`, `"BACK"`, `"FIRST"`, `"LAST"`) |
| `TableName` | `string` | The `tableName` originally passed to `SetGridView()` |
| `RouteName` | `string` | The `routeName` originally passed to `SetGridView()` |

---

## Search Integration

`GridView` automatically generates a JavaScript function named `{tableName}_SearchEvent` and injects it into the page alongside the pagination controls.

Call it from your own search handler with an object whose **keys match your controller action's parameter names**:

```javascript
// tableName = "htmlTableUsers"  →  function name = htmlTableUsers_SearchEvent
function SearchEvent() {
    htmlTableUsers_SearchEvent({
        search: document.getElementById('Search').value,
        test:   "any extra query param"
    });
}
```

The function fires a `GET` request to your controller with the supplied parameters as a query string, then replaces the table div with the response — keeping search and pagination in sync without a full page reload.

To preserve the active search term across pagination clicks, save it to session in the `GET` action and retrieve it in the `POST` action:

```csharp
// GET
HttpContext.Session.SetString("Search", search);

// POST
string search = HttpContext.Session.GetString("Search") ?? string.Empty;
```

---

## CSS Customisation

All CSS classes are configurable before calling `SetGridView()`:

```csharp
gridView.CSS_Pagination = "pagination justify-content-center";
gridView.CSS_Button     = "page-item page-link btn rounded-0";
gridView.CSS_PageIndex  = "bg-success text-white"; // active page highlight
```

The component is designed to work out of the box with **Bootstrap 4/5** pagination classes, but any CSS framework can be used.

---

## Notes & Gotchas

- **`tableName` must match the HTML element `id`** — the injected JS locates the element by this id to perform the partial swap.
- **`routeName` must match the `[Route("...")]` attribute** on your POST action — they are used to construct the fetch URL.
- **Always wrap the row loop in `try/catch`** in your view — during the first render `start` and `end` may briefly be out of range while the model is initialised.
- **`page_entry` and `pagination` must be rendered outside the `try/catch`** so they always appear regardless of whether rows rendered successfully.
- **Session is required.** `GridView` stores `rowSize`, `pageSize`, `pageSequence`, and `FirstAndLast` in session so they persist across the stateless POST requests. Ensure `app.UseSession()` is called before `app.MapControllerRoute()`.
- **Return `PartialView` from the POST action**, not `View`. The JS fetch extracts only the inner HTML of the table div, but returning `PartialView` avoids double-rendering layout chrome.

---

## Breaking Changes & Migration

### v2.0.0 → v3.0.0 — Property Renames

All `GridView` output properties were renamed from `lowercase` to `PascalCase` to follow C# conventions. If you are upgrading from v1.0.0, update your View accordingly:

| v2.0.0 (old) | v3.0.0 (new) |
|---|---|
| `Model.start` | `Model.Start` |
| `Model.end` | `Model.End` |
| `Model.table[i]` | `Model.Table` |
| `Model.page_entry` | `Model.PageEntry` |
| `Model.pagination` | `Model.Pagination` |

**Updated view loop after migration:**

`Model.Table` is typed as `object`, so you must cast it before indexing. Declare a typed variable at the top of your `@{ }` block and use that in the loop:

```razor
@{
    var table = Model.Table as List<users_table.dataDTO>;

    try // YOU MUST USE TRY CATCH HERE
    {
        for (int i = Model.Start; i < Model.End; i++)
        {
            <tr>
                <td>@table[i].id</td>
                <td>@table[i].firstname</td>
                <td>@table[i].lastname</td>
                <td>@table[i].age</td>
                <td>@table[i].datecreated</td>
            </tr>
        }
    }
    catch { }
    // THE PAGE ENTRY AND PAGINATION MUST ALWAYS BE BELOW OR ABOVE THE TRY CATCH
    <tr>
        <td colspan="5">
            <div>@Model.PageEntry</div>
            <div>@Model.Pagination</div>
        </td>
    </tr>
}
```

> Replace `users_table.dataDTO` with your own DTO type.

> **Symptom if not updated:** The pagination buttons will appear and work correctly, but no table rows will be rendered — because `Model.start` and `Model.end` silently resolve to `0` inside the `try/catch`.

---

## Resources

### GitHub Repository

| Repository | Description |
|---|---|
| [BNet.ASP.MVC.Pagination](https://github.com/bryanjaybodino/BNet.Library/tree/master/BNet.ASP.MVC.Pagination) | Core library source code |
| [BNet.ASP.MVC.Pagination.Sample](https://github.com/bryanjaybodino/BNet.Library/tree/master/BNet.ASP.MVC.Pagination.Sample) | Full working sample project |

### Video Tutorial

[![Tagalog Tutorial v1.0.0](https://img.shields.io/badge/YouTube-Tagalog%20Tutorial%20v1.0.0-red?logo=youtube)](https://www.youtube.com/watch?v=Y4ki37Uof1o)

> Step-by-step walkthrough in Tagalog covering setup, controller configuration, and view integration for version 1.0.0.