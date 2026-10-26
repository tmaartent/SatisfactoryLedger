# Coming from Java / Spring

The concepts are the same. The names, and where things live, differ. Use this page as a translation table.

## Build & project structure

| Java / Maven / Gradle | .NET | Notes |
|---|---|---|
| JDK | .NET SDK | `dotnet --version` |
| JVM / bytecode | CLR (runtime) / IL | — |
| `mvn` / `gradle` | `dotnet` CLI (MSBuild underneath) | `dotnet build`, `test`, `run`, `add package` |
| `pom.xml` / `build.gradle` | `.csproj` | Much shorter. No plugins section for normal use. |
| Multi-module build | Solution (`.slnx`) + projects | Each project = one module = one assembly |
| Parent POM | `Directory.Build.props` | Applies automatically to all projects below it |
| `<dependencyManagement>` / BOM | `Directory.Packages.props` | Central Package Management |
| Maven Central | nuget.org | Check downloads, last update and **license** before adding |
| `.jar` | assembly (`.dll`) / NuGet package (`.nupkg`) | — |
| `package com.x.y` | `namespace X.Y;` | Folders aren't enforced, but convention mirrors them |
| package-private / JPMS `exports` | `internal` (visible within the assembly only) | The key tool for module boundaries |
| test access to internals | `InternalsVisibleTo` | Declared in the csproj |
| Spring Boot DevTools | `dotnet watch` | Hot reload |
| docker-compose for local dev | Aspire AppHost | Plus a dashboard with logs and traces |

## Spring Boot → ASP.NET Core

| Spring | ASP.NET Core | Notes |
|---|---|---|
| Spring Boot app / `main` | `Program.cs` with `WebApplication.CreateBuilder` | Everything is wired explicitly in this one file. Little magic. |
| `@RestController` + `@GetMapping` | Minimal APIs: `app.MapGet("/x", handler)` (or controllers) | We use minimal APIs with route groups (`MapGroup`) |
| `@Component`/`@Service` + component scanning | `builder.Services.AddScoped<IFoo, Foo>()` | **No component scanning by default.** You register explicitly. Each module gets one `AddXxxModule()` extension method. |
| `@Autowired` / constructor injection | Constructor injection, or parameters on a minimal API handler | Same idea |
| Scopes: singleton / request / prototype | `AddSingleton` / `AddScoped` (per request) / `AddTransient` | — |
| `application.yml` | `appsettings.json` (+ `appsettings.Development.json`) | — |
| `@ConfigurationProperties` | `IOptions<T>` / `builder.Services.Configure<T>()` | — |
| Spring profiles | Environments (`ASPNETCORE_ENVIRONMENT`) | `app.Environment.IsDevelopment()` |
| Servlet filters / `HandlerInterceptor` | Middleware (`app.Use...`) / endpoint filters | — |
| `@ControllerAdvice` | `IExceptionHandler` + `AddProblemDetails()` | — |
| Bean Validation (`@NotNull`) | Data annotations (`[Required]`) + `AddValidation()`, or FluentValidation | — |
| SLF4J / Logback | `ILogger<T>` (Microsoft.Extensions.Logging) | Message templates: `"Factory {Id}"`, never `$"..."` |
| Actuator / Micrometer | Health checks + OpenTelemetry | Already wired in ServiceDefaults |
| springdoc-openapi + Swagger UI | `Microsoft.AspNetCore.OpenApi` + Scalar | — |

## Persistence: JPA/Hibernate → EF Core

| JPA / Spring Data | EF Core | Notes |
|---|---|---|
| Hibernate | EF Core | — |
| `EntityManager` / persistence context | `DbContext` | Also the unit of work: change tracking + `SaveChangesAsync()` |
| `JpaRepository<T, ID>` | `DbSet<T>` on the DbContext | A repository on top is optional (chapter 04) |
| JPQL / Criteria API | LINQ (`db.Blueprints.Where(b => ...)`) | Translated into SQL |
| `@Entity` / annotations | Fluent configuration in `IEntityTypeConfiguration<T>` | Keep the domain free of attributes |
| `@Embeddable` | Complex types / owned types | For value objects like `Rate` |
| `@Version` | Concurrency token / row version | → `ETag` in the API |
| `@Transactional` | `SaveChangesAsync` is one transaction. Explicit: `Database.BeginTransactionAsync()`. | No annotation magic |
| Flyway / Liquibase | EF Core migrations (`dotnet ef migrations add X`) | Generated from your model |

## Testing

| Java | .NET | Notes |
|---|---|---|
| JUnit 5 `@Test` | xUnit `[Fact]` | — |
| `@ParameterizedTest` + `@CsvSource` | `[Theory]` + `[InlineData(...)]` / `[MemberData]` | — |
| `@BeforeEach` / `@AfterEach` | Test class constructor / `IDisposable` (`IAsyncLifetime` for async) | xUnit creates a new instance per test |
| `@BeforeAll` / shared fixture | `IClassFixture<T>` / `ICollectionFixture<T>` | E.g. one PostgreSQL container per run |
| AssertJ | Shouldly or AwesomeAssertions | FluentAssertions v8+ needs a commercial license. Plain `Assert.Equal` also works. |
| Mockito | NSubstitute | — |
| jqwik | CsCheck | Property-based testing |
| **ArchUnit** | **ArchUnitNET** | A direct port. Familiar API. |
| Testcontainers | Testcontainers for .NET | Same concept |
| `@SpringBootTest` + MockMvc | `WebApplicationFactory<Program>` + `HttpClient` | Starts the API in memory |

## Language: C# vs Java

| Java | C# | Watch out |
|---|---|---|
| getters/setters, Lombok | **Properties**: `public int Height { get; private set; }`, `init`, `required` | No Lombok needed |
| `record` | `record` (class) and `record struct` | Value equality for free, ideal for value objects |
| `Optional<T>` | Nullable reference types: `string?` | Compile-time checks only |
| Streams | LINQ (`Select`, `Where`, `Sum`) | Deferred execution, like streams |
| `CompletableFuture` | `Task` + `async`/`await` | Async methods end in `Async` by convention |
| try-with-resources | `using var x = ...;` | — |
| Rich `enum` with fields and methods | C# `enum` is **just a named int** | For rich enums, use a record or class with static instances |
| No operator overloading | **Operator overloading** | `Rate a + Rate b` is possible and fits value objects well |
| Type erasure | Generics are **reified** | `typeof(T)` works at runtime |
| Checked exceptions | None | We use result types for business failures (chapter 02) |
| — | `struct` (value type) | `default(MyStruct)` exists without a constructor call. Mind invariants (the `Rate` with denominator 0 trap). |
| — | Extension methods | How `AddXxxModule()` is attached to `IServiceCollection` |
| Naming: `camelCase` methods | **PascalCase** methods and properties, `IFoo` interfaces, `_camelCase` private fields | — |

## Where to find things

- **Official docs:** learn.microsoft.com, the .NET and ASP.NET Core sections. Searching "\<topic\> site:learn.microsoft.com" works better than general search.
- **API reference:** learn.microsoft.com/dotnet/api. **Framework source:** source.dot.net.
- **In Rider:**
  - Ctrl+Click goes into the (decompiled or source-linked) framework code.
  - Shift Shift opens *Search Everywhere*.
  - Alt+F7 shows *Find Usages*.
  - The NuGet tool window browses packages.
- **Reference implementations worth reading:**
  - `dotnet/eShop` on GitHub: Microsoft's sample app, with Aspire.
  - `kgrzybek/modular-monolith-with-ddd`: the best-known DDD modular monolith in .NET. It's an older .NET version, but the structure maps directly onto ours.
- **Blogs:** Andrew Lock (".NET Escapades") for ASP.NET Core in depth, and Milan Jovanović for DDD, modular monoliths and EF Core in modern .NET.
