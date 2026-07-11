-- ==========================================
-- HYBRID ERP DATABASE SCHEMA (PostgreSQL / Supabase)
-- ==========================================

-- 0. Users Table (For authentication & authorization)
CREATE TABLE IF NOT EXISTS users (
    id SERIAL PRIMARY KEY,
    username VARCHAR(100) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    role VARCHAR(50) DEFAULT 'USER' CHECK (role IN ('SUPER_ADMIN', 'USER')),
    is_approved BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- 1. Suppliers Table (Shared for Fabrics and Trading Purchases)
CREATE TABLE IF NOT EXISTS suppliers (
    id SERIAL PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    contact_number VARCHAR(50),
    email VARCHAR(100),
    address TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- 2. Products Table
-- Classified as MANUFACTURED or TRADED
CREATE TABLE IF NOT EXISTS products (
    id SERIAL PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    category VARCHAR(100) NOT NULL, -- e.g., 'Shirt', 'Jeans', 'T-Shirt'
    product_type VARCHAR(50) NOT NULL CHECK (product_type IN ('MANUFACTURED', 'TRADED')),
    design_brand VARCHAR(255),
    size VARCHAR(50) NOT NULL,       -- e.g., 'S', 'M', 'L', 'XL', '32', '34'
    color VARCHAR(50) NOT NULL,      -- e.g., 'Blue', 'Black', 'White'
    cost_price NUMERIC(10, 2) DEFAULT 0.00,  -- For TRADED: purchase price; For MANUFACTURED: production cost per piece
    selling_price NUMERIC(10, 2) DEFAULT 0.00,
    gst_percent NUMERIC(5, 2) DEFAULT 18.00, -- Default GST is 18% in apparel typically
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- 3. Fabrics Table (Raw materials for manufacturing)
CREATE TABLE IF NOT EXISTS fabrics (
    id SERIAL PRIMARY KEY,
    name VARCHAR(255) NOT NULL,      -- e.g., 'Cotton Twill', 'Linen Blend'
    color VARCHAR(100) NOT NULL,
    supplier_id INT REFERENCES suppliers(id) ON DELETE SET NULL,
    cost_per_meter NUMERIC(10, 2) NOT NULL,
    total_meters NUMERIC(10, 2) NOT NULL,
    used_meters NUMERIC(10, 2) DEFAULT 0.00,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- 4. Production Batches Table (For internal shirt manufacturing)
CREATE TABLE IF NOT EXISTS production_batches (
    id SERIAL PRIMARY KEY,
    batch_code VARCHAR(100) UNIQUE NOT NULL,
    fabric_id INT REFERENCES fabrics(id) ON DELETE RESTRICT,
    fabric_meters_used NUMERIC(10, 2) NOT NULL,
    wastage_meters NUMERIC(10, 2) DEFAULT 0.00,
    tailoring_cost NUMERIC(10, 2) DEFAULT 0.00,
    additional_cost NUMERIC(10, 2) DEFAULT 0.00, -- Buttons, thread, label, washing, etc.
    quantity_produced INT DEFAULT 0,              -- Number of shirts/items created
    cost_per_piece NUMERIC(10, 2) DEFAULT 0.00,   -- Calculated dynamically upon completion
    status VARCHAR(50) DEFAULT 'PLANNING' CHECK (status IN ('PLANNING', 'IN_PRODUCTION', 'COMPLETED')),
    product_id INT REFERENCES products(id) ON DELETE RESTRICT, -- The target manufactured product
    date_created TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    date_completed TIMESTAMP WITH TIME ZONE
);

-- 5. Trading Purchases Table (Buying ready-made products from external suppliers)
CREATE TABLE IF NOT EXISTS trading_purchases (
    id SERIAL PRIMARY KEY,
    invoice_no VARCHAR(100) NOT NULL,
    supplier_id INT REFERENCES suppliers(id) ON DELETE RESTRICT,
    product_id INT REFERENCES products(id) ON DELETE RESTRICT,
    quantity INT NOT NULL CHECK (quantity > 0),
    purchase_price NUMERIC(10, 2) NOT NULL,
    gst_percent NUMERIC(5, 2) DEFAULT 18.00,
    total_cost NUMERIC(10, 2) NOT NULL,          -- Calculated as (quantity * purchase_price) + GST
    purchase_date TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- 6. Sales Orders Table
CREATE TABLE IF NOT EXISTS sales_orders (
    id SERIAL PRIMARY KEY,
    invoice_no VARCHAR(100) UNIQUE NOT NULL,
    customer_name VARCHAR(255) NOT NULL,
    customer_phone VARCHAR(50),
    total_amount NUMERIC(10, 2) NOT NULL,        -- Total before tax and discounts
    total_gst NUMERIC(10, 2) NOT NULL,           -- Total tax amount
    final_amount NUMERIC(10, 2) NOT NULL,        -- Amount payable (total + gst)
    sales_date TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- 7. Sales Order Items Table
-- Handles both SET and PCS sales
CREATE TABLE IF NOT EXISTS sales_order_items (
    id SERIAL PRIMARY KEY,
    sales_order_id INT REFERENCES sales_orders(id) ON DELETE CASCADE,
    product_id INT REFERENCES products(id) ON DELETE RESTRICT,
    item_type VARCHAR(50) NOT NULL CHECK (item_type IN ('SET', 'PCS')), -- SET sale or PCS sale
    quantity INT NOT NULL CHECK (quantity > 0),    -- For SET: number of sets; For PCS: number of pieces
    unit_price NUMERIC(10, 2) NOT NULL,            -- Price sold at
    gst_percent NUMERIC(5, 2) NOT NULL,
    discount NUMERIC(10, 2) DEFAULT 0.00,
    sub_total NUMERIC(10, 2) NOT NULL             -- Final line amount
);

-- 8. Unified Stock Ledger Table
-- Records every inventory movement
CREATE TABLE IF NOT EXISTS stock_ledger (
    id SERIAL PRIMARY KEY,
    product_id INT REFERENCES products(id) ON DELETE CASCADE,
    quantity_change INT NOT NULL,                 -- Positive for stock-in, negative for sales
    transaction_type VARCHAR(50) NOT NULL CHECK (transaction_type IN ('MANUFACTURED_IN', 'TRADING_IN', 'SALE_OUT', 'ADJUSTMENT')),
    reference_id INT,                             -- Production Batch ID, Trading Purchase ID, Sales Order ID
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- ==========================================
-- VIEWS FOR REPORTS & INVENTORY
-- ==========================================

-- A. Current Unified Stock View
CREATE OR REPLACE VIEW vw_stock_summary AS
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
CREATE OR REPLACE VIEW vw_profit_report AS
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
    (p.cost_price * (CASE WHEN soi.item_type = 'SET' THEN soi.quantity * 4 ELSE soi.quantity END)) AS total_cost_basis, -- Assuming 4 items per Set for profit calculation sample (actual logic will be set by user or code)
    (soi.sub_total - (soi.gst_percent / 100.0 * soi.sub_total) - (p.cost_price * (CASE WHEN soi.item_type = 'SET' THEN soi.quantity * 4 ELSE soi.quantity END))) AS net_profit
FROM sales_order_items soi
JOIN sales_orders so ON soi.sales_order_id = so.id
JOIN products p ON soi.product_id = p.id;

-- ==========================================
-- SAMPLE SEED DATA
-- ==========================================

-- Insert Suppliers
INSERT INTO suppliers (name, contact_number, email, address) VALUES
('TexMill Fabrics', '+1234567890', 'sales@texmill.com', '12 Fabric Lane, Textile Zone'),
('ReadyWear Imports', '+0987654321', 'info@readywear.com', '45 Trading Street, Port Area');

-- Insert Products
-- Manufactured Products (Standard Shirts in S, M, L)
INSERT INTO products (name, category, product_type, design_brand, size, color, cost_price, selling_price, gst_percent) VALUES
('Classic Cotton Shirt', 'Shirt', 'MANUFACTURED', 'OwnBrand', 'S', 'Blue', 180.00, 450.00, 12.00),
('Classic Cotton Shirt', 'Shirt', 'MANUFACTURED', 'OwnBrand', 'M', 'Blue', 180.00, 450.00, 12.00),
('Classic Cotton Shirt', 'Shirt', 'MANUFACTURED', 'OwnBrand', 'L', 'Blue', 180.00, 450.00, 12.00);

-- Traded Products (Ready-made Jeans)
INSERT INTO products (name, category, product_type, design_brand, size, color, cost_price, selling_price, gst_percent) VALUES
('Slim Fit Denim Jeans', 'Jeans', 'TRADED', 'DenimCo', '32', 'Black', 350.00, 899.00, 12.00),
('Slim Fit Denim Jeans', 'Jeans', 'TRADED', 'DenimCo', '34', 'Black', 350.00, 899.00, 12.00);

-- Insert Fabric
INSERT INTO fabrics (name, color, supplier_id, cost_per_meter, total_meters, used_meters) VALUES
('Cotton Twill Fabric', 'Blue', 1, 60.00, 500.00, 150.00);

-- Insert Production Batch
-- To produce 100 shirts of size M
INSERT INTO production_batches (batch_code, fabric_id, fabric_meters_used, wastage_meters, tailoring_cost, additional_cost, quantity_produced, cost_per_piece, status, product_id, date_completed) VALUES
('BATCH-2026-001', 1, 150.00, 5.00, 5000.00, 4000.00, 100, 180.00, 'COMPLETED', 2, CURRENT_TIMESTAMP);

-- Insert Purchase (Trading)
INSERT INTO trading_purchases (invoice_no, supplier_id, product_id, quantity, purchase_price, gst_percent, total_cost) VALUES
('INV-TR-9901', 2, 4, 50, 350.00, 12.00, 19600.00), -- 50 jeans of size 32 (350 * 50 = 17500 + 12% GST = 19600)
('INV-TR-9901', 2, 5, 50, 350.00, 12.00, 19600.00); -- 50 jeans of size 34

-- Record Stock Entry for Manufactured & Traded Items
INSERT INTO stock_ledger (product_id, quantity_change, transaction_type, reference_id) VALUES
(2, 100, 'MANUFACTURED_IN', 1), -- 100 shirts of size M from BATCH-2026-001
(4, 50, 'TRADING_IN', 1),     -- 50 jeans from TRADING purchase 1
(5, 50, 'TRADING_IN', 2);     -- 50 jeans from TRADING purchase 2
