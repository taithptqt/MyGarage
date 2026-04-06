SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRAN;

    DECLARE @Ho TABLE
    (
        Idx INT NOT NULL PRIMARY KEY,
        Val NVARCHAR(30) NOT NULL
    );

    DECLARE @Dem TABLE
    (
        Idx INT NOT NULL PRIMARY KEY,
        Val NVARCHAR(30) NOT NULL
    );

    DECLARE @Ten TABLE
    (
        Idx INT NOT NULL PRIMARY KEY,
        Val NVARCHAR(30) NOT NULL
    );

    INSERT INTO @Ho (Idx, Val)
    VALUES
        (1, N'Nguyễn'),
        (2, N'Trần'),
        (3, N'Lê'),
        (4, N'Phạm'),
        (5, N'Hoàng'),
        (6, N'Vũ'),
        (7, N'Đặng'),
        (8, N'Bùi'),
        (9, N'Đỗ'),
        (10, N'Hồ');

    INSERT INTO @Dem (Idx, Val)
    VALUES
        (1, N'Văn'),
        (2, N'Đức'),
        (3, N'Gia'),
        (4, N'Minh'),
        (5, N'Thanh'),
        (6, N'Quang'),
        (7, N'Nhật'),
        (8, N'Khánh'),
        (9, N'Anh'),
        (10, N'Bảo');

    INSERT INTO @Ten (Idx, Val)
    VALUES
        (1, N'An'),
        (2, N'Bình'),
        (3, N'Cường'),
        (4, N'Dũng'),
        (5, N'Hải'),
        (6, N'Hùng'),
        (7, N'Khoa'),
        (8, N'Long'),
        (9, N'Nam'),
        (10, N'Phúc'),
        (11, N'Phong'),
        (12, N'Quân'),
        (13, N'Sơn'),
        (14, N'Thành'),
        (15, N'Thắng'),
        (16, N'Trung'),
        (17, N'Tuấn'),
        (18, N'Việt'),
        (19, N'Vinh'),
        (20, N'Tú');

    ;WITH SeededStaff AS
    (
        SELECT s.Id,
               s.BranchId,
               s.TeamId,
               s.Email,
               ROW_NUMBER() OVER (ORDER BY s.BranchId, s.TeamId, s.Id) AS RN
        FROM dbo.Staffs AS s
        WHERE ISNULL(s.IsDeleted, 0) = 0
          AND UPPER(ISNULL(s.Status, 'ACTIVE')) = 'ACTIVE'
          AND s.Email LIKE 'tech.[0-9].%@mygarage.local'
    ),
    NameBase AS
    (
        SELECT h.Val + N' ' + d.Val + N' ' + t.Val AS FullName,
               ((h.Idx - 1) * 200 + (d.Idx - 1) * 20 + (t.Idx - 1)) AS BaseIndex
        FROM @Ho AS h
        CROSS JOIN @Dem AS d
        CROSS JOIN @Ten AS t
    ),
    NamePool AS
    (
        SELECT ROW_NUMBER() OVER (
                   ORDER BY ((nb.BaseIndex * 37 + 113) % 2000), nb.BaseIndex
               ) AS RN,
               nb.FullName
        FROM NameBase AS nb
    )
    UPDATE s
       SET s.FullName = np.FullName,
           s.Phone =
                CONCAT('09', RIGHT(CONCAT('00000000', CAST(ss.Id AS VARCHAR(8))), 8)),
           s.UpdatedAt = GETDATE()
    FROM dbo.Staffs AS s
    INNER JOIN SeededStaff AS ss
        ON ss.Id = s.Id
    INNER JOIN NamePool AS np
        ON np.RN = ss.RN;

    COMMIT TRAN;

    SELECT s.Id,
           s.BranchId,
           t.Code AS TeamCode,
           s.FullName,
           s.Phone,
           s.Email
    FROM dbo.Staffs AS s
    LEFT JOIN dbo.Teams AS t
        ON t.Id = s.TeamId
    WHERE ISNULL(s.IsDeleted, 0) = 0
      AND UPPER(ISNULL(s.Status, 'ACTIVE')) = 'ACTIVE'
      AND s.Email LIKE 'tech.[0-9].%@mygarage.local'
    ORDER BY s.BranchId, t.Code, s.Id;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRAN;
    THROW;
END CATCH;
