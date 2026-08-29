# CloudAdvisor — Intelligent Cloud Infrastructure Advisory System

CloudAdvisor is an ASP.NET Core MVC web application that performs deep static code analysis on uploaded project ZIP files to identify software patterns and automatically recommend a tailored, cost-optimized AWS deployment roadmap.

## Project Flow & Use Case

CloudAdvisor is designed to bridge the gap between software development and cloud deployment. Often, developers build applications but struggle with mapping their architecture to the most cost-effective and appropriate cloud services. CloudAdvisor solves this by automating the recommendation process.

### **The Typical User Flow**

1. **User Authentication (Login & Registration)**
   - **Action:** A user creates an account or logs in (supports standard JWT authentication).
   - **Use Case:** Secures the platform and ensures that uploaded projects and analysis histories are tied to the specific user.

2. **Project Upload**
   - **Action:** From the dashboard, the user navigates to the analysis section and uploads a `.zip` file of their C# project.
   - **Use Case:** The system provides a drag-and-drop interface with validation to securely receive the source code for inspection, avoiding path traversal vulnerabilities.

3. **Deep Static Analysis**
   - **Action:** Once uploaded, the system decompresses the ZIP and uses the Microsoft.CodeAnalysis (Roslyn SDK) to scan the C# abstract syntax trees.
   - **Use Case:** It looks for specific architectural patterns, dependency injection configurations, namespaces, and data access methods (e.g., Entity Framework usage, caching mechanisms).

4. **AWS Infrastructure Recommendation**
   - **Action:** Based on the analyzed codebase, the recommendation engine maps patterns to corresponding AWS services (e.g., EC2, RDS, S3, Lambda, ElastiCache).
   - **Use Case:** Converts complex code architectures into actionable cloud deployment topologies, minimizing guesswork.

5. **Cost Estimation & Dashboarding**
   - **Action:** The system queries current unit pricing for the recommended services and generates graphical breakdowns (monthly/annual).
   - **Use Case:** Gives users a clear financial perspective of the required cloud infrastructure, allowing them to budget and plan effectively before any actual deployment.

6. **Reporting**
   - **Action:** The user can view the analysis summary and download an advisory report.
   - **Use Case:** Provides a portable, standardized document that developers can share with DevOps teams or stakeholders.

### **The Admin Flow**

1. **Service Catalog Management**
   - **Action:** An Administrator accesses a specialized admin panel.
   - **Use Case:** Allows the admin to enable/disable specific AWS services in the catalog and update their unit pricing dynamically (via asynchronous AJAX requests). The recommendation engine will adapt in real-time to these configuration changes.

2. **Platform Monitoring**
   - **Action:** Admins can view aggregate system usage and history logs of all analyses performed.
   - **Use Case:** Helps track platform usage, popular code patterns among users, and maintain the application's overall health.

---

## Technical Architecture

The application is built using a **Clean Architecture pattern with a Services Layer**:

- **Controllers**:
  - `AuthController` / `AccountController`: Manages user registration, login, and Google OAuth integrations.
  - `HomeController` / `DashboardController`: Renders landing pages, user dashboards, and recent history records.
  - `ProjectController` / `AnalysisController`: Manages uploading, processing pipeline execution, results rendering, and advisory report downloads.
  - `AdminController`: Aggregates usage statistics, manages the active AWS service catalog, and supports histories log management.
- **Services**:
  - `FileExtractorService`: Handles zip decompression, directory enumeration, and implements security mitigations against Zip Slip.
  - `RoslynAnalysisService` / `CodeAnalysisService`: Compiles C# trees into a Roslyn workspace to resolve semantic metadata symbols and uses syntactic heuristics.
  - `RecommendationEngine`: Evaluates rules by checking database service configs.
  - `CostEstimationService`: Compiles cost tallies and plain-text summaries.
- **Data / Database Context**:
  - `ApplicationDbContext`: Configures Entity Framework Core mapping classes (`AnalysisHistory`, `AwsService`, `User`) and seeds the initial data.

---

## Getting Started & Setup

### Prerequisites
- **.NET 8.0 SDK** (or later)
- **Microsoft SQL Server LocalDB** (standard dev instance `(localdb)\mssqllocaldb`)
- **Visual Studio 2022** or **VS Code**

### Configuration
The database connection string is configured in `appsettings.json`. By default, it targets SQL Server LocalDB:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=CloudAdvisorDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
}
```

### Installation & Run Steps

1. **Restore dependencies**:
   ```bash
   dotnet restore
   ```
2. **Apply migrations and update database**:
   The application automatically runs migrations on startup and seeds an admin user! If you wish to apply migrations manually from the package manager console, use:
   ```bash
   # Command Line:
   dotnet ef database update
   ```
3. **Run the application**:
   ```bash
   dotnet run
   ```
4. **Access the application**:
   Open your browser and navigate to `https://localhost:5001` or `http://localhost:5000` (or the port specified by Kestrel in the console logs).
   *Note: An Admin user is automatically seeded on the first startup if none exists.*

---

## Developer Commands

If you make modifications to the Entity models, use these commands to keep the database in sync:

```bash
# Add a new migration:
dotnet ef migrations add <MigrationName>

# Remove the last migration:
dotnet ef migrations remove
```
