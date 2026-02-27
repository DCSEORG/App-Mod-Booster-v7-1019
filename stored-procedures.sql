/*
  stored-procedures.sql
  Stored procedures for the Expense Management application.
  Uses CREATE OR ALTER PROCEDURE syntax so this script can be run multiple times.
*/

SET NOCOUNT ON;
GO

-- ============================================================
-- usp_GetAllExpenses
-- Retrieves all expenses with optional filtering
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.usp_GetAllExpenses
    @UserId   INT = NULL,
    @StatusId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        e.ExpenseId,
        e.UserId,
        u.UserName,
        u.Email        AS UserEmail,
        e.CategoryId,
        c.CategoryName,
        e.StatusId,
        s.StatusName,
        e.AmountMinor,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        rv.UserName    AS ReviewedByName,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    JOIN dbo.Users u              ON e.UserId = u.UserId
    JOIN dbo.ExpenseCategories c  ON e.CategoryId = c.CategoryId
    JOIN dbo.ExpenseStatus s      ON e.StatusId = s.StatusId
    LEFT JOIN dbo.Users rv        ON e.ReviewedBy = rv.UserId
    WHERE
        (@UserId   IS NULL OR e.UserId   = @UserId)
        AND (@StatusId IS NULL OR e.StatusId = @StatusId)
    ORDER BY e.CreatedAt DESC;
END
GO

-- ============================================================
-- usp_GetExpenseById
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.usp_GetExpenseById
    @ExpenseId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        e.ExpenseId,
        e.UserId,
        u.UserName,
        u.Email        AS UserEmail,
        e.CategoryId,
        c.CategoryName,
        e.StatusId,
        s.StatusName,
        e.AmountMinor,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        rv.UserName    AS ReviewedByName,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    JOIN dbo.Users u              ON e.UserId = u.UserId
    JOIN dbo.ExpenseCategories c  ON e.CategoryId = c.CategoryId
    JOIN dbo.ExpenseStatus s      ON e.StatusId = s.StatusId
    LEFT JOIN dbo.Users rv        ON e.ReviewedBy = rv.UserId
    WHERE e.ExpenseId = @ExpenseId;
END
GO

-- ============================================================
-- usp_CreateExpense
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.usp_CreateExpense
    @UserId      INT,
    @CategoryId  INT,
    @AmountMinor INT,
    @Currency    NVARCHAR(3)   = 'GBP',
    @ExpenseDate DATE,
    @Description NVARCHAR(1000) = NULL,
    @ReceiptFile NVARCHAR(500)  = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- New expenses start as Draft
    DECLARE @DraftStatusId INT = (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Draft');

    INSERT INTO dbo.Expenses
        (UserId, CategoryId, StatusId, AmountMinor, Currency, ExpenseDate, Description, ReceiptFile, CreatedAt)
    VALUES
        (@UserId, @CategoryId, @DraftStatusId, @AmountMinor, @Currency, @ExpenseDate, @Description, @ReceiptFile, SYSUTCDATETIME());

    SELECT SCOPE_IDENTITY() AS NewExpenseId;
END
GO

-- ============================================================
-- usp_UpdateExpenseStatus
-- Handles submit, approve, and reject actions
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.usp_UpdateExpenseStatus
    @ExpenseId   INT,
    @NewStatus   NVARCHAR(50),   -- 'Submitted', 'Approved', 'Rejected'
    @ReviewedBy  INT = NULL,     -- Required for Approved/Rejected
    @Notes       NVARCHAR(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @StatusId INT = (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = @NewStatus);

    IF @StatusId IS NULL
    BEGIN
        RAISERROR('Invalid status: %s', 16, 1, @NewStatus);
        RETURN;
    END

    UPDATE dbo.Expenses
    SET
        StatusId    = @StatusId,
        SubmittedAt = CASE WHEN @NewStatus = 'Submitted' THEN SYSUTCDATETIME() ELSE SubmittedAt END,
        ReviewedBy  = CASE WHEN @NewStatus IN ('Approved', 'Rejected') THEN @ReviewedBy ELSE ReviewedBy END,
        ReviewedAt  = CASE WHEN @NewStatus IN ('Approved', 'Rejected') THEN SYSUTCDATETIME() ELSE ReviewedAt END
    WHERE ExpenseId = @ExpenseId;

    SELECT @@ROWCOUNT AS RowsAffected;
END
GO

-- ============================================================
-- usp_DeleteExpense
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.usp_DeleteExpense
    @ExpenseId INT
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM dbo.Expenses WHERE ExpenseId = @ExpenseId;
    SELECT @@ROWCOUNT AS RowsAffected;
END
GO

-- ============================================================
-- usp_GetAllUsers
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.usp_GetAllUsers
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        u.UserId,
        u.UserName,
        u.Email,
        u.RoleId,
        r.RoleName,
        u.ManagerId,
        m.UserName AS ManagerName,
        u.IsActive,
        u.CreatedAt
    FROM dbo.Users u
    JOIN dbo.Roles r       ON u.RoleId = r.RoleId
    LEFT JOIN dbo.Users m  ON u.ManagerId = m.UserId
    WHERE u.IsActive = 1
    ORDER BY u.UserName;
END
GO

-- ============================================================
-- usp_GetUserById
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.usp_GetUserById
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        u.UserId,
        u.UserName,
        u.Email,
        u.RoleId,
        r.RoleName,
        u.ManagerId,
        m.UserName AS ManagerName,
        u.IsActive,
        u.CreatedAt
    FROM dbo.Users u
    JOIN dbo.Roles r       ON u.RoleId = r.RoleId
    LEFT JOIN dbo.Users m  ON u.ManagerId = m.UserId
    WHERE u.UserId = @UserId;
END
GO

-- ============================================================
-- usp_GetAllCategories
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.usp_GetAllCategories
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        CategoryId,
        CategoryName,
        IsActive
    FROM dbo.ExpenseCategories
    WHERE IsActive = 1
    ORDER BY CategoryName;
END
GO

-- ============================================================
-- usp_GetExpenseStats
-- Returns summary statistics for the dashboard
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.usp_GetExpenseStats
    @UserId INT = NULL  -- Optional: filter by user
AS
BEGIN
    SET NOCOUNT ON;

    -- Counts by status
    SELECT
        s.StatusName,
        COUNT(e.ExpenseId)              AS Count,
        ISNULL(SUM(e.AmountMinor), 0)   AS TotalAmountMinor
    FROM dbo.ExpenseStatus s
    LEFT JOIN dbo.Expenses e ON e.StatusId = s.StatusId
        AND (@UserId IS NULL OR e.UserId = @UserId)
    GROUP BY s.StatusId, s.StatusName
    ORDER BY s.StatusId;

    -- Overall totals
    SELECT
        COUNT(*)                            AS TotalExpenses,
        ISNULL(SUM(AmountMinor), 0)         AS TotalAmountMinor,
        ISNULL(SUM(CASE WHEN StatusId = (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Submitted') THEN AmountMinor ELSE 0 END), 0) AS PendingAmountMinor
    FROM dbo.Expenses
    WHERE (@UserId IS NULL OR UserId = @UserId);
END
GO
