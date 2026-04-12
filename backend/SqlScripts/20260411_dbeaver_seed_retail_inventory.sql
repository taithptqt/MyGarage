/*
  MyGarage — Seed tồn kho retail (DBeaver / Azure SQL, không dùng GO)

  Lỗi trước đây: Id kho = 1 không có trong dbo.Warehouses → B báo lỗi nhưng D vẫn MERGE → FK.
  Cách mới:
  - @ManualWarehouseId = NULL → tự lấy 1 kho active (ưu tiên Type có RETAIL / SELLABLE).
  - @ManualWarehouseId = số → dùng đúng Id đó (copy từ kết quả SELECT phần A).
*/

SET NOCOUNT ON;

/* ========== A) Xem bảng + danh sách kho (chỉ đọc) ========== */

SELECT t.TABLE_SCHEMA, t.TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES AS t
WHERE t.TABLE_TYPE = N'BASE TABLE'
  AND (
        t.TABLE_NAME LIKE N'%Inventor%'
     OR t.TABLE_NAME LIKE N'Warehouses'
     OR t.TABLE_NAME LIKE N'Products'
  )
ORDER BY t.TABLE_NAME;

SELECT
    w.Id           AS WarehouseId,
    w.Code,
    w.Name,
    w.Type,
    w.BranchId,
    w.IsActive,
    w.IsDeleted
FROM dbo.Warehouses AS w
WHERE (w.IsDeleted = 0 OR w.IsDeleted IS NULL)
ORDER BY w.BranchId, w.Id;

SELECT
    w.Id,
    w.Code,
    w.Name,
    w.Type,
    COUNT(i.Id)     AS InventoryRows,
    SUM(i.Quantity) AS SumQuantity
FROM dbo.Warehouses AS w
LEFT JOIN dbo.Inventories AS i ON i.WarehouseId = w.Id
WHERE (w.IsDeleted = 0 OR w.IsDeleted IS NULL)
GROUP BY w.Id, w.Code, w.Name, w.Type
ORDER BY w.Id;

/* ========== Thiết lập kho dùng cho B / C / D / E ========== */

DECLARE @ManualWarehouseId BIGINT = NULL; /* ĐỔI thành Id thật nếu không muốn tự chọn; NULL = tự động */

DECLARE @WhId BIGINT = COALESCE(
    @ManualWarehouseId,
    (
        SELECT TOP (1) w.Id
        FROM dbo.Warehouses AS w
        WHERE (w.IsDeleted = 0 OR w.IsDeleted IS NULL)
          AND (w.IsActive = 1 OR w.IsActive IS NULL)
        ORDER BY
            CASE
                WHEN UPPER(LTRIM(RTRIM(ISNULL(w.Type, N'')))) LIKE N'%RETAIL%'
                  OR UPPER(LTRIM(RTRIM(ISNULL(w.Type, N'')))) LIKE N'%SELLABLE%'
                THEN 0
                ELSE 1
            END,
            w.Id
    )
);

IF @WhId IS NULL
BEGIN
    THROW 50001, N'Không có kho trong Warehouses (active, chưa xóa). Hãy tạo kho hoặc gán @ManualWarehouseId = Id từ SELECT phần A.', 1;
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Warehouses WHERE Id = @WhId AND (IsDeleted = 0 OR IsDeleted IS NULL))
BEGIN
    THROW 50002, N'@ManualWarehouseId không hợp lệ hoặc kho đã xóa.', 1;
END;

SELECT @WhId AS UsingWarehouseId; /* In ra Id đang dùng — đối chiếu với phần A */

/* ========== B) MERGE: mọi sản phẩm active → cùng mức tồn ========== */

DECLARE @B_DefaultQty DECIMAL(18, 3) = 20; /* ĐỔI nếu cần */

;MERGE dbo.Inventories AS tgt
USING (
    SELECT p.Id AS ProductId
    FROM dbo.Products AS p
    WHERE (p.IsDeleted = 0 OR p.IsDeleted IS NULL)
      AND (p.IsActive = 1 OR p.IsActive IS NULL)
) AS src
ON tgt.WarehouseId = @WhId AND tgt.ProductId = src.ProductId
WHEN MATCHED THEN
    UPDATE SET
        tgt.Quantity = @B_DefaultQty,
        tgt.ReservedQuantity = ISNULL(tgt.ReservedQuantity, 0)
WHEN NOT MATCHED BY TARGET THEN
    INSERT (
        WarehouseId,
        ProductId,
        Quantity,
        MinStockLevel,
        MaxStockLevel,
        LocationBin,
        ReservedQuantity,
        Current_MAC_Price,
        TotalInventoryValue
    )
    VALUES (
        @WhId,
        src.ProductId,
        @B_DefaultQty,
        0,
        NULL,
        NULL,
        0,
        0,
        0
    );

/* ========== C) Chỉ sửa dòng Quantity = 0 (không INSERT) ========== */

DECLARE @C_FillQty DECIMAL(18, 3) = 15;

UPDATE i
SET i.Quantity = @C_FillQty
FROM dbo.Inventories AS i
WHERE i.WarehouseId = @WhId
  AND i.Quantity = 0;

/* ========== D) Map theo SKU ========== */

IF OBJECT_ID(N'tempdb..#Map') IS NOT NULL DROP TABLE #Map;

CREATE TABLE #Map (
    SKU NVARCHAR(50) COLLATE DATABASE_DEFAULT NOT NULL PRIMARY KEY,
    Qty DECIMAL(18, 3) NOT NULL
);

INSERT INTO #Map (SKU, Qty) VALUES
    (N'OIL-MOB-0W40-4L', 30),
    (N'FLT-OIL-TOY-S', 50),
    (N'BRK-BOS-DOT4-1L', 100);

;MERGE dbo.Inventories AS tgt
USING (
    SELECT p.Id AS ProductId, m.Qty
    FROM #Map AS m
    INNER JOIN dbo.Products AS p ON p.SKU = m.SKU
    WHERE (p.IsDeleted = 0 OR p.IsDeleted IS NULL)
) AS src
ON tgt.WarehouseId = @WhId AND tgt.ProductId = src.ProductId
WHEN MATCHED THEN
    UPDATE SET tgt.Quantity = src.Qty, tgt.ReservedQuantity = ISNULL(tgt.ReservedQuantity, 0)
WHEN NOT MATCHED BY TARGET THEN
    INSERT (
        WarehouseId,
        ProductId,
        Quantity,
        MinStockLevel,
        MaxStockLevel,
        LocationBin,
        ReservedQuantity,
        Current_MAC_Price,
        TotalInventoryValue
    )
    VALUES (
        @WhId,
        src.ProductId,
        src.Qty,
        0,
        NULL,
        NULL,
        0,
        0,
        0
    );

DROP TABLE #Map;

/* ========== E) Kiểm tra ========== */

SELECT TOP 50
    p.Id,
    p.SKU,
    p.Name,
    i.Quantity,
    i.ReservedQuantity,
    (i.Quantity - ISNULL(i.ReservedQuantity, 0)) AS AvailableApprox
FROM dbo.Inventories AS i
INNER JOIN dbo.Products AS p ON p.Id = i.ProductId
WHERE i.WarehouseId = @WhId
ORDER BY p.Id;
