# Custom Keyboard Builder - Practical WPF Project Structure

This document maps the analysis artifacts in `Documents` to a practical WPF C# project structure backed by SQL Server/SSMS.

## Layers

| Folder | Purpose |
| --- | --- |
| `Models` | Domain/entity classes based on ERD and class diagram. |
| `ViewModels` | MVVM state and commands for WPF screens. |
| `Views` | WPF UserControls/Windows for Buyer, Seller, Admin screens. |
| `Services` | Business workflows from FHD/DFD: account, build, request, admin operations. |
| `Repositories` | Data access contracts. SQL Server implementations can be added here. |
| `Data/SqlServer` | SQL Server connection/settings helpers. |
| `Database/SqlServer` | Scripts to run in SSMS. |

## Code Milestones

1. Create SQL Server database using `Database/SqlServer/CreateSchema.sql`.
2. Add a SQL Client package when package restore is available: `Microsoft.Data.SqlClient`.
3. Implement repository classes against SQL Server.
4. Build Buyer flow, Seller request handling, and Admin management against real ViewModels/database data.
5. Keep optional realtime/chat work in separate phases so it does not block the core MVP.
