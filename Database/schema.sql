-- Mini Order Management System — schema and sample data
-- Target: MySQL 8.x
--
-- Run with:  mysql -u <user> -p order_management < schema.sql
-- (create the database first, e.g. CREATE DATABASE order_management;)

CREATE TABLE IF NOT EXISTS Products (
    Id              INT AUTO_INCREMENT PRIMARY KEY,
    Name            VARCHAR(200)    NOT NULL,
    Sku             VARCHAR(50)     NOT NULL,
    Description     VARCHAR(500)    NULL,
    Price           DECIMAL(18,2)   NOT NULL,
    StockQuantity   INT             NOT NULL DEFAULT 0,
    CONSTRAINT UQ_Products_Sku UNIQUE (Sku)
) CHARACTER SET utf8mb4;

CREATE TABLE IF NOT EXISTS Orders (
    Id          INT AUTO_INCREMENT PRIMARY KEY,
    OrderDate   DATETIME NOT NULL
) CHARACTER SET utf8mb4;

CREATE TABLE IF NOT EXISTS OrderItems (
    Id          INT AUTO_INCREMENT PRIMARY KEY,
    OrderId     INT             NOT NULL,
    ProductId   INT             NOT NULL,
    Quantity    INT             NOT NULL,
    UnitPrice   DECIMAL(18,2)   NOT NULL,
    CONSTRAINT FK_OrderItems_Orders
        FOREIGN KEY (OrderId) REFERENCES Orders (Id) ON DELETE CASCADE,
    CONSTRAINT FK_OrderItems_Products
        FOREIGN KEY (ProductId) REFERENCES Products (Id) ON DELETE RESTRICT,
    INDEX IX_OrderItems_OrderId (OrderId),
    INDEX IX_OrderItems_ProductId (ProductId)
) CHARACTER SET utf8mb4;

-- Sample products
INSERT INTO Products (Name, Sku, Description, Price, StockQuantity) VALUES
    ('Wireless Mouse',      'WM-001',  'Ergonomic 2.4GHz wireless mouse',        19.99,  100),
    ('Mechanical Keyboard', 'KB-002',  'Tenkeyless mechanical keyboard, blue switches', 49.99, 50),
    ('USB-C Hub',           'HUB-003', '7-in-1 USB-C hub with HDMI and card reader', 24.50, 75),
    ('27-inch Monitor',     'MON-004', '27" 1440p IPS monitor',                  189.00, 20),
    ('Webcam 1080p',        'CAM-005', 'Full HD USB webcam with autofocus',      34.75,  40),
    ('Laptop Stand',        'LS-006',  'Adjustable aluminium laptop stand',      15.20,  60);
