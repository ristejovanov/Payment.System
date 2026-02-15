## 🧱 Security.Migrator

**Security.Migrator** is a console-based utility application designed to create or update the database used by **Security.API**.
It applies Entity Framework migrations and ensures the database schema is always up to date.
The tool can run in three flexible modes — **interactive**, **configuration-based**, or **parameter-based** — depending on your environment setup.

---

### 🚀 Usage Modes

#### 🧩 1️⃣ Interactive Mode (no arguments, no configuration)

If you simply **run the application without any arguments** and without a configured connection string,
the tool automatically starts in **interactive console mode**.

```
Security.DbMigrator.exe
```

You will be prompted to enter:

* SQL Server name
* Database name
* Windows authentication (Y/N)
* Username and password (if applicable)

During password entry, characters are masked for security.
Once you confirm, the tool will connect, display pending migrations, and ask:

```
Press 'Y' to apply pending migrations, or any other key to cancel.
```

After confirmation, the database will be created or updated.

---

#### ⚙️ 2️⃣ Configuration Mode (via `appsettings.json`)

If your `appsettings.json` contains a **DefaultConnection** string,
the migrator will automatically detect and use it — no user input required.

Example `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=SecurityDB;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

To run using configuration mode:

```
Security.DbMigrator.exe
```

The tool will detect the connection string, print a masked version (e.g., with password hidden as `*****`),
and apply migrations automatically.

---

#### 🧭 3️⃣ Command-Line Mode (custom arguments)

You can also provide connection parameters directly via **command-line arguments** — useful for automation, CI/CD pipelines, or manual scripting.

**Syntax:**

```
Security.DbMigrator.exe -ds:<server> -c:<database> -u:<user> -p:<password>
```

**Example:**

```
Security.DbMigrator.exe -ds:localhost -c:SecurityDB -u:sa -p:MyStrongPassword
```

or provide the full connection string directly:

```
Security.DbMigrator.exe -cs:"Server=localhost;Database=SecurityDB;User Id=sa;Password=MyStrongPassword;"
```

All sensitive values (like passwords) are masked in console output for safety.

---

### 🧾 Migration Summary Output

After connecting, the tool prints:

* ✅ Applied migrations
* ➜ Pending migrations
* Confirmation prompt before execution
* Success or failure summary

Example:

```
Applied migrations:
  ✔ 20241010120000_InitialCreate

Pending migrations:
  ➜ 20241012100000_AddClientsTable

Press 'Y' to apply pending migrations, or any other key to cancel.

Applying migrations...
✅ Migration completed successfully!
```

---

### 🔁 Regeneration Behavior

If the **Security.API** application is started after the migrator initializes the database:

* It will automatically generate the **API key** and create a default **Admin Client** record.
* If the API key file (`security/apikey.txt`) is deleted or lost, simply delete the **Admin Client** record in the database and restart **Security.API** — a new key will be generated automatically.

---

### 🧰 Example Scenarios

| Scenario                  | Action                                                                 |
| ------------------------- | ---------------------------------------------------------------------- |
| Developer running locally | Just run `Security.DbMigrator.exe` interactively                       |
| Deployment pipeline       | Use `-cs:"connection string"` in PowerShell or CI job                  |
| Config-based environment  | Store connection in `appsettings.json` and run without arguments       |
| First-time setup          | Run once interactively, then launch `Security.API` to generate API key |

---

### ✅ Key Points

* Runs safely even with missing inputs.
* Masks all passwords in console output.
* Automatically detects connection order:
  `CLI args → appsettings.json → interactive input`.
* Clean exit and clear user messages for each step.
* Database migrations are applied asynchronously for stability.
