namespace SuperMarket.Identity.Infrastructure.Data.Seeding;

/// <summary>
/// Static, immutable catalog defining the baseline identity dataset:
/// atomic permissions, permission packages (groups), and default system roles
/// derived from the domain specifications in roles.drawio.
/// </summary>
public static class IdentitySeedCatalog
{
    public sealed record PermissionDefinition(
        string Resource,
        string Action,
        string Scope,
        string Category)
    {
        public string Key => $"{Resource.ToLowerInvariant()}:{Action.ToLowerInvariant()}:{Scope.ToLowerInvariant()}";
    }

    public sealed record PermissionGroupDefinition(
        string GroupName,
        string Description,
        IReadOnlyList<string> PermissionKeys);

    public sealed record RoleDefinition(
        string RoleName,
        string Description,
        IReadOnlyList<string> GroupNames);

    // =========================================================================
    // 1. Atomic Permissions (30+ domain permissions)
    // =========================================================================
    public static readonly IReadOnlyList<PermissionDefinition> Permissions = new List<PermissionDefinition>
    {
        // --- System Admin: Users Management ---
        new("users", "create", "all", "User Management"),
        new("users", "update", "all", "User Management"),
        new("users", "activate", "all", "User Management"),
        new("users", "deactivate", "all", "User Management"),
        new("users", "delete", "all", "User Management"),
        new("users", "reset-password", "all", "User Management"),

        // --- System Admin: Roles Management ---
        new("roles", "create", "all", "Role Management"),
        new("roles", "update", "all", "Role Management"),
        new("roles", "delete", "all", "Role Management"),

        // --- System Admin: Branches Management ---
        new("branches", "create", "all", "Branch Management"),
        new("branches", "update", "all", "Branch Management"),
        new("branches", "activate", "all", "Branch Management"),
        new("branches", "deactivate", "all", "Branch Management"),
        new("branches", "transfer-manager", "all", "Branch Management"),

        // --- System Admin: System Setup ---
        new("settings", "tax-currency", "manage", "System Configuration"),
        new("settings", "notifications", "manage", "System Configuration"),
        new("settings", "payment-gateways", "manage", "System Configuration"),
        new("settings", "receipt-templates", "manage", "System Configuration"),

        // --- System Admin: Monitoring & Audit ---
        new("monitoring", "branches-data", "view", "System Monitoring"),
        new("monitoring", "audit-logs", "view", "System Monitoring"),
        new("monitoring", "error-logs", "view", "System Monitoring"),
        new("monitoring", "system-metrics", "view", "System Monitoring"),
        new("monitoring", "alerts", "manage", "System Monitoring"),

        // --- System Admin: Backup ---
        new("backup", "create", "manual", "Database Backup"),
        new("backup", "schedule", "manage", "Database Backup"),
        new("backup", "restore", "execute", "Database Backup"),
        new("backup", "export", "data", "Database Backup"),

        // --- Store Manager: Branch Employees ---
        new("branch-employees", "create", "branch", "Branch Staff"),
        new("branch-employees", "update", "branch", "Branch Staff"),
        new("branch-employees", "status", "branch", "Branch Staff"),
        new("branch-employees", "reset-password", "branch", "Branch Staff"),
        new("branch-employees", "assign-role", "branch", "Branch Staff"),

        // --- Store Manager: Sales & Approvals ---
        new("sales", "view", "branch", "Branch Sales"),
        new("sales", "void", "branch", "Branch Sales"),
        new("returns", "approve", "branch", "Branch Sales"),
        new("discounts", "override-limit", "branch", "Branch Sales"),
        new("sales-reports", "export", "branch", "Branch Sales"),

        // --- Store Manager: Branch Stock ---
        new("inventory", "view", "branch", "Branch Inventory"),
        new("inventory", "low-stock", "view", "Branch Inventory"),
        new("inventory", "transfer-request", "branch", "Branch Inventory"),
        new("inventory", "adjustment-approve", "branch", "Branch Inventory"),
        new("inventory", "movements", "view", "Branch Inventory"),

        // --- Store Manager: Shifts Oversight ---
        new("shifts", "reports", "branch", "Branch Shifts"),
        new("shifts", "discrepancies", "branch", "Branch Shifts"),
        new("shifts", "emergency-control", "branch", "Branch Shifts"),
        new("cashiers", "performance", "view", "Branch Shifts"),

        // --- Store Manager: Reports & Settings ---
        new("reports", "sales", "branch", "Branch Reports"),
        new("reports", "top-selling", "branch", "Branch Reports"),
        new("reports", "cashiers", "branch", "Branch Reports"),
        new("reports", "stock", "branch", "Branch Reports"),
        new("reports", "shifts", "branch", "Branch Reports"),
        new("reports", "export", "branch", "Branch Reports"),
        new("branch-settings", "update", "branch", "Branch Settings"),
        new("branch-hours", "update", "branch", "Branch Settings"),
        new("branch-receipt", "update", "branch", "Branch Settings"),
        new("branch-printing", "manage", "branch", "Branch Settings"),
        new("notifications", "store-alerts", "receive", "Branch Notifications"),
        new("notifications", "staff-broadcast", "send", "Branch Notifications"),

        // --- Cashier: Shifts Operations ---
        new("shifts", "open", "own", "Shift Operations"),
        new("shifts", "close", "own", "Shift Operations"),
        new("shifts", "suspend", "own", "Shift Operations"),

        // --- Cashier: POS Sales Operations ---
        new("pos", "products", "scan-search", "POS Operations"),
        new("pos", "cart", "manage", "POS Operations"),
        new("pos", "discount", "apply-standard", "POS Operations"),
        new("pos", "bills", "hold-retrieve", "POS Operations"),

        // --- Cashier: Payments ---
        new("payments", "process", "all-methods", "POS Payments"),
        new("payments", "complete", "execute", "POS Payments"),

        // --- Cashier: Post-Sale & Returns ---
        new("receipts", "print", "own", "POS Receipts"),
        new("sales", "record", "own", "Sales Tracking"),
        new("returns", "request", "own", "Customer Returns"),

        // --- Inventory Manager: Product Catalog ---
        new("products", "create", "all", "Product Catalog"),
        new("products", "update", "all", "Product Catalog"),
        new("products", "pricing", "manage", "Product Catalog"),
        new("products", "status", "manage", "Product Catalog"),

        // --- Inventory Manager: Stock Control & Transfers ---
        new("inventory", "stock", "view-all", "Stock Control"),
        new("inventory", "stock", "add-deduct", "Stock Control"),
        new("inventory", "stock", "adjust", "Stock Control"),
        new("inventory", "transfers", "manage", "Stock Control"),

        // --- Inventory Manager: Physical Stocktake ---
        new("stocktake", "task", "create", "Stocktake"),
        new("stocktake", "counts", "record", "Stocktake"),
        new("stocktake", "discrepancies", "review", "Stocktake"),
        new("stocktake", "reports", "print", "Stocktake"),

        // --- Inventory Manager: Alerts, Barcodes & Reports ---
        new("stock-alerts", "monitor", "all", "Stock Alerts"),
        new("stock-alerts", "thresholds", "manage", "Stock Alerts"),
        new("barcodes", "labels", "print", "Barcode Operations"),
        new("inventory-reports", "view-export", "all", "Inventory Reports"),

        // --- Purchasing Manager: Suppliers ---
        new("suppliers", "create", "all", "Supplier Management"),
        new("suppliers", "update", "all", "Supplier Management"),
        new("suppliers", "status-delete", "manage", "Supplier Management"),
        new("suppliers", "performance", "rate", "Supplier Management"),

        // --- Purchasing Manager: Purchase Orders & Goods Receiving ---
        new("purchase-orders", "create", "all", "Purchase Orders"),
        new("purchase-orders", "update", "all", "Purchase Orders"),
        new("purchase-orders", "manage-status", "all", "Purchase Orders"),
        new("goods-receipts", "record", "all", "Goods Receiving"),
        new("goods-receipts", "discrepancies", "record", "Goods Receiving"),
        new("goods-receipts", "close-po", "all", "Goods Receiving"),
        new("supplier-payments", "request-view", "all", "Supplier Invoices"),
        new("purchasing-reports", "view-export", "all", "Purchasing Reports"),
        new("purchasing-alerts", "monitor", "all", "Purchasing Alerts"),

        // --- Accountant: Financial Settlement & Auditing ---
        new("financial", "shifts-review", "all", "Financial Reconciliation"),
        new("financial", "shifts-settle", "all", "Financial Reconciliation"),
        new("financial", "payments", "reconcile-all", "Financial Settlements"),
        new("financial", "supplier-payments", "execute", "Financial Settlements"),
        new("financial", "returns-review", "manage", "Financial Auditing"),
        new("financial-reports", "view-export", "all", "Financial Reports"),
        new("financial-audit", "logs", "view", "Financial Audit"),
        new("financial-audit", "inventory-ledger", "view", "Financial Audit"),
        new("financial-alerts", "monitor", "all", "Financial Alerts")
    };

    // =========================================================================
    // 2. Permission Groups (The 6 Standard Packages)
    // =========================================================================
    public static readonly IReadOnlyList<PermissionGroupDefinition> PermissionGroups = new List<PermissionGroupDefinition>
    {
        new(
            GroupName: "System Administration Package",
            Description: "Full administrative access: user management, roles, branches, system settings, backups, and monitoring.",
            PermissionKeys: new[]
            {
                "users:create:all", "users:update:all", "users:activate:all", "users:deactivate:all", "users:delete:all", "users:reset-password:all",
                "roles:create:all", "roles:update:all", "roles:delete:all",
                "branches:create:all", "branches:update:all", "branches:activate:all", "branches:deactivate:all", "branches:transfer-manager:all",
                "settings:tax-currency:manage", "settings:notifications:manage", "settings:payment-gateways:manage", "settings:receipt-templates:manage",
                "monitoring:branches-data:view", "monitoring:audit-logs:view", "monitoring:error-logs:view", "monitoring:system-metrics:view", "monitoring:alerts:manage",
                "backup:create:manual", "backup:schedule:manage", "backup:restore:execute", "backup:export:data"
            }),

        new(
            GroupName: "Store Management Package",
            Description: "Branch operations management: staff control, sales overrides, stock requests, shift audits, and branch settings.",
            PermissionKeys: new[]
            {
                "branch-employees:create:branch", "branch-employees:update:branch", "branch-employees:status:branch", "branch-employees:reset-password:branch", "branch-employees:assign-role:branch",
                "sales:view:branch", "sales:void:branch", "returns:approve:branch", "discounts:override-limit:branch", "sales-reports:export:branch",
                "inventory:view:branch", "inventory:low-stock:view", "inventory:transfer-request:branch", "inventory:adjustment-approve:branch", "inventory:movements:view",
                "shifts:reports:branch", "shifts:discrepancies:branch", "shifts:emergency-control:branch", "cashiers:performance:view",
                "reports:sales:branch", "reports:top-selling:branch", "reports:cashiers:branch", "reports:stock:branch", "reports:shifts:branch", "reports:export:branch",
                "branch-settings:update:branch", "branch-hours:update:branch", "branch-receipt:update:branch", "branch-printing:manage:branch",
                "notifications:store-alerts:receive", "notifications:staff-broadcast:send"
            }),

        new(
            GroupName: "Cashier Core Package",
            Description: "Front-desk POS terminal operations: shifts, item scanning, billing, payments, receipts, and returns.",
            PermissionKeys: new[]
            {
                "shifts:open:own", "shifts:close:own", "shifts:suspend:own",
                "pos:products:scan-search", "pos:cart:manage", "pos:discount:apply-standard", "pos:bills:hold-retrieve",
                "payments:process:all-methods", "payments:complete:execute",
                "receipts:print:own", "sales:record:own", "returns:request:own"
            }),

        new(
            GroupName: "Inventory Operations Package",
            Description: "Warehouse & catalog control: products, stock levels, physical stocktakes, transfer execution, and barcode labels.",
            PermissionKeys: new[]
            {
                "products:create:all", "products:update:all", "products:pricing:manage", "products:status:manage",
                "inventory:stock:view-all", "inventory:stock:add-deduct", "inventory:stock:adjust", "inventory:transfers:manage",
                "stocktake:task:create", "stocktake:counts:record", "stocktake:discrepancies:review", "stocktake:reports:print",
                "stock-alerts:monitor:all", "stock-alerts:thresholds:manage",
                "barcodes:labels:print", "inventory-reports:view-export:all"
            }),

        new(
            GroupName: "Purchasing & Supply Package",
            Description: "Procurement management: supplier directory, purchase orders, goods receipts, and payment requests.",
            PermissionKeys: new[]
            {
                "suppliers:create:all", "suppliers:update:all", "suppliers:status-delete:manage", "suppliers:performance:rate",
                "purchase-orders:create:all", "purchase-orders:update:all", "purchase-orders:manage-status:all",
                "goods-receipts:record:all", "goods-receipts:discrepancies:record", "goods-receipts:close-po:all",
                "supplier-payments:request-view:all", "purchasing-reports:view-export:all", "purchasing-alerts:monitor:all"
            }),

        new(
            GroupName: "Financial Accounting Package",
            Description: "Fiscal reconciliation: shift discrepancies, payment settlements, tax/P&L reports, and financial audit logs.",
            PermissionKeys: new[]
            {
                "financial:shifts-review:all", "financial:shifts-settle:all",
                "financial:payments:reconcile-all", "financial:supplier-payments:execute",
                "financial:returns-review:manage", "financial-reports:view-export:all",
                "financial-audit:logs:view", "financial-audit:inventory-ledger:view", "financial-alerts:monitor:all"
            })
    };

    // =========================================================================
    // 3. Default System Roles (The 6 Roles Mapped to Packages)
    // =========================================================================
    public static readonly IReadOnlyList<RoleDefinition> Roles = new List<RoleDefinition>
    {
        new(
            RoleName: "System Admin",
            Description: "Chief system administrator with full platform control and configuration authority.",
            GroupNames: new[] { "System Administration Package" }),

        new(
            RoleName: "Store Manager",
            Description: "Branch executive overseeing staff, floor operations, inventory, and shift approvals.",
            GroupNames: new[] { "Store Management Package" }),

        new(
            RoleName: "Cashier",
            Description: "Point of sale operator responsible for front-line transactions, shifts, and cash handling.",
            GroupNames: new[] { "Cashier Core Package" }),

        new(
            RoleName: "Inventory Manager",
            Description: "Warehouse officer managing product catalogs, stock balances, stocktakes, and logistics.",
            GroupNames: new[] { "Inventory Operations Package" }),

        new(
            RoleName: "Purchasing Manager",
            Description: "Supply chain officer managing vendors, purchase orders, and inbound shipments.",
            GroupNames: new[] { "Purchasing & Supply Package" }),

        new(
            RoleName: "Accountant",
            Description: "Financial officer reviewing shift ledgers, fiscal compliance, supplier payments, and audit books.",
            GroupNames: new[] { "Financial Accounting Package" })
    };
}
