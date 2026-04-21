/* Post-checks after dropping legacy TotalDiscount columns. */

SET NOCOUNT ON;

SELECT
    'Column_Quotations_TotalDiscount_Exists' AS CheckName,
    COUNT(1) AS CheckValue
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Quotations'
  AND COLUMN_NAME = 'TotalDiscount';

SELECT
    'Column_ServiceOrders_TotalDiscount_Exists' AS CheckName,
    COUNT(1) AS CheckValue
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'ServiceOrders'
  AND COLUMN_NAME = 'TotalDiscount';

SELECT
    'QuotationDiscounts_Rows' AS CheckName,
    COUNT(1) AS CheckValue
FROM dbo.QuotationDiscounts;

SELECT
    'ServiceOrderDiscounts_Rows' AS CheckName,
    COUNT(1) AS CheckValue
FROM dbo.ServiceOrderDiscounts;
