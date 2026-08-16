-- ==========================================
-- APPAREL ERP DATABASE SCHEMA (SQLite Local / Desktop)
-- ==========================================

-- 0. Users Table
CREATE TABLE IF NOT EXISTS users (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    username TEXT UNIQUE NOT NULL,
    password_hash TEXT NOT NULL,
    role TEXT DEFAULT 'USER',
    is_approved INTEGER DEFAULT 0,
    created_at TEXT DEFAULT CURRENT_TIMESTAMP
);

-- 1. Suppliers Table
CREATE TABLE IF NOT EXISTS suppliers (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    contact_number TEXT,
    email TEXT,
    address TEXT,
    created_at TEXT DEFAULT CURRENT_TIMESTAMP
);

-- 2. Products Table
CREATE TABLE IF NOT EXISTS products (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    category TEXT NOT NULL,
    product_type TEXT NOT NULL,
    design_brand TEXT,
    size TEXT NOT NULL,
    color TEXT NOT NULL,
    cost_price REAL DEFAULT 0.00,
    selling_price REAL DEFAULT 0.00,
    gst_percent REAL DEFAULT 18.00,
    created_at TEXT DEFAULT CURRENT_TIMESTAMP
);

-- 3. Fabrics Table
CREATE TABLE IF NOT EXISTS fabrics (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    color TEXT NOT NULL,
    supplier_id INTEGER REFERENCES suppliers(id) ON DELETE SET NULL,
    cost_per_meter REAL NOT NULL,
    total_meters REAL NOT NULL,
    used_meters REAL DEFAULT 0.00,
    is_deleted INTEGER DEFAULT 0,
    created_at TEXT DEFAULT CURRENT_TIMESTAMP
);

-- 4. Production Batches Table
CREATE TABLE IF NOT EXISTS production_batches (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    batch_code TEXT UNIQUE NOT NULL,
    fabric_id INTEGER REFERENCES fabrics(id),
    fabric_meters_used REAL NOT NULL,
    wastage_meters REAL DEFAULT 0.00,
    tailoring_cost REAL DEFAULT 0.00,
    additional_cost REAL DEFAULT 0.00,
    quantity_produced INTEGER DEFAULT 0,
    cost_per_piece REAL DEFAULT 0.00,
    status TEXT DEFAULT 'PLANNING',
    product_id INTEGER REFERENCES products(id),
    design_name TEXT,
    quantity_to_sew INTEGER DEFAULT 0,
    date_created TEXT DEFAULT CURRENT_TIMESTAMP,
    date_completed TEXT
);

-- 5. Trading Purchases Table
CREATE TABLE IF NOT EXISTS trading_purchases (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    invoice_no TEXT NOT NULL,
    supplier_id INTEGER REFERENCES suppliers(id),
    product_id INTEGER REFERENCES products(id),
    quantity INTEGER NOT NULL,
    purchase_price REAL NOT NULL,
    gst_percent REAL DEFAULT 18.00,
    total_cost REAL NOT NULL,
    purchase_date TEXT DEFAULT CURRENT_TIMESTAMP
);

-- 6. Sales Orders Table
CREATE TABLE IF NOT EXISTS sales_orders (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    invoice_no TEXT UNIQUE NOT NULL,
    customer_name TEXT NOT NULL,
    customer_phone TEXT,
    total_amount REAL NOT NULL,
    total_gst REAL NOT NULL,
    final_amount REAL NOT NULL,
    sales_date TEXT DEFAULT CURRENT_TIMESTAMP
);

-- 7. Sales Order Items Table
CREATE TABLE IF NOT EXISTS sales_order_items (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    sales_order_id INTEGER REFERENCES sales_orders(id) ON DELETE CASCADE,
    product_id INTEGER REFERENCES products(id),
    item_type TEXT NOT NULL,
    quantity INTEGER NOT NULL,
    unit_price REAL NOT NULL,
    gst_percent REAL NOT NULL,
    discount REAL DEFAULT 0.00,
    sub_total REAL NOT NULL
);

-- 8. Unified Stock Ledger Table
CREATE TABLE IF NOT EXISTS stock_ledger (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    product_id INTEGER REFERENCES products(id) ON DELETE CASCADE,
    quantity_change INTEGER NOT NULL,
    transaction_type TEXT NOT NULL,
    reference_id INTEGER,
    created_at TEXT DEFAULT CURRENT_TIMESTAMP
);

-- 9. Operating Expenses Table
CREATE TABLE IF NOT EXISTS expenses (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    title TEXT NOT NULL,
    category TEXT NOT NULL,
    amount REAL NOT NULL,
    expense_date TEXT DEFAULT CURRENT_TIMESTAMP,
    payment_mode TEXT DEFAULT 'CASH',
    notes TEXT,
    created_at TEXT DEFAULT CURRENT_TIMESTAMP
);

-- ==========================================
-- VIEWS FOR REPORTS & INVENTORY (SQLite)
-- ==========================================

-- A. Current Unified Stock View
CREATE VIEW IF NOT EXISTS vw_stock_summary AS
SELECT 
    p.id AS product_id,
    p.name AS product_name,
    p.category,
    p.product_type,
    p.design_brand,
    p.size,
    p.color,
    p.cost_price,
    p.selling_price,
    COALESCE(SUM(sl.quantity_change), 0) AS current_stock
FROM products p
LEFT JOIN stock_ledger sl ON p.id = sl.product_id
GROUP BY p.id, p.name, p.category, p.product_type, p.design_brand, p.size, p.color, p.cost_price, p.selling_price;

-- B. Profit Dashboard View
CREATE VIEW IF NOT EXISTS vw_profit_report AS
SELECT 
    so.id AS sales_order_id,
    so.invoice_no,
    so.customer_name,
    so.sales_date,
    p.id AS product_id,
    p.name AS product_name,
    p.product_type,
    p.category,
    p.size,
    p.color,
    soi.item_type,
    soi.quantity AS quantity_sold,
    soi.unit_price AS selling_price_per_unit,
    soi.sub_total AS item_revenue,
    p.cost_price AS cost_price_per_unit,
    (p.cost_price * (CASE WHEN soi.item_type = 'SET' THEN soi.quantity * 4 ELSE soi.quantity END)) AS total_cost_basis,
    (soi.sub_total - (soi.gst_percent / 100.0 * soi.sub_total) - (p.cost_price * (CASE WHEN soi.item_type = 'SET' THEN soi.quantity * 4 ELSE soi.quantity END))) AS net_profit
FROM sales_order_items soi
JOIN sales_orders so ON soi.sales_order_id = so.id
JOIN products p ON soi.product_id = p.id;
