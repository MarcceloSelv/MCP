-- Script de teste para validar diversos tipos de comandos SQL Server

-- ========================================
-- DML (Data Manipulation Language)
-- ========================================

-- SELECT básico
SELECT * FROM Users WHERE Id = 1;

-- SELECT com JOINs
SELECT u.Name, o.OrderDate, p.ProductName
FROM Users u
INNER JOIN Orders o ON u.Id = o.UserId
LEFT JOIN OrderItems oi ON o.Id = oi.OrderId
INNER JOIN Products p ON oi.ProductId = p.Id
WHERE u.Active = 1
ORDER BY o.OrderDate DESC;

-- INSERT
INSERT INTO Users (Name, Email, CreatedAt)
VALUES ('John Doe', 'john@example.com', GETDATE());

-- UPDATE
UPDATE Users 
SET Email = 'newemail@example.com',
    UpdatedAt = GETDATE()
WHERE Id = 1;

-- DELETE
DELETE FROM Users WHERE Id = 999;

-- MERGE
MERGE INTO Target t
USING Source s ON t.Id = s.Id
WHEN MATCHED THEN UPDATE SET t.Value = s.Value
WHEN NOT MATCHED THEN INSERT (Id, Value) VALUES (s.Id, s.Value);

-- ========================================
-- DDL (Data Definition Language)
-- ========================================

-- CREATE TABLE
CREATE TABLE Customers (
    Id INT PRIMARY KEY IDENTITY(1,1),
    Name NVARCHAR(100) NOT NULL,
    Email NVARCHAR(255),
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    CONSTRAINT UQ_Email UNIQUE (Email)
);

-- ALTER TABLE
ALTER TABLE Users 
ADD PhoneNumber VARCHAR(20) NULL,
    Address NVARCHAR(500) NULL;

-- CREATE INDEX
CREATE NONCLUSTERED INDEX IX_Users_Email 
ON Users (Email)
INCLUDE (Name);

-- CREATE VIEW
CREATE VIEW ActiveUsers AS
SELECT Id, Name, Email
FROM Users
WHERE Active = 1;

-- CREATE PROCEDURE
CREATE PROCEDURE GetUserById
    @UserId INT
AS
BEGIN
    SELECT * FROM Users WHERE Id = @UserId;
END;

-- CREATE FUNCTION
CREATE FUNCTION dbo.GetUserCount()
RETURNS INT
AS
BEGIN
    DECLARE @Count INT;
    SELECT @Count = COUNT(*) FROM Users;
    RETURN @Count;
END;

-- CREATE TRIGGER
CREATE TRIGGER trg_Users_Audit
ON Users
AFTER INSERT, UPDATE
AS
BEGIN
    INSERT INTO AuditLog (TableName, Action, ActionDate)
    SELECT 'Users', 'INSERT/UPDATE', GETDATE()
    FROM inserted;
END;

-- ========================================
-- DBCC Commands
-- ========================================

DBCC USEROPTIONS;
DBCC CHECKDB (MyDatabase);
DBCC FREEPROCCACHE;
DBCC DROPCLEANBUFFERS;
DBCC SQLPERF(LOGSPACE);

-- ========================================
-- Extended Events
-- ========================================

CREATE EVENT SESSION [QueryPerformance] ON SERVER 
ADD EVENT sqlserver.sql_statement_completed(
    ACTION(sqlserver.client_app_name,sqlserver.database_name)
    WHERE ([duration]>(1000000))
)
ADD TARGET package0.event_file(SET filename=N'C:\Temp\QueryPerf.xel')
WITH (MAX_MEMORY=4096 KB, EVENT_RETENTION_MODE=ALLOW_SINGLE_EVENT_LOSS);

ALTER EVENT SESSION [QueryPerformance] ON SERVER STATE = START;

-- ========================================
-- XML Methods
-- ========================================

DECLARE @TestXML XML = '<event><data name="cpu_time"><value>1500</value></data></event>';

SELECT @TestXML.value('(/event/data[@name="cpu_time"]/value)[1]', 'bigint') AS CpuTime;

SELECT @TestXML.query('//data[@name="cpu_time"]') AS CpuNode;

-- ========================================
-- JSON
-- ========================================

SELECT * FROM Users
FOR JSON AUTO;

SELECT * FROM OPENJSON('{"name":"John","age":30}')
WITH (
    name NVARCHAR(50),
    age INT
);

-- ========================================
-- CTE (Common Table Expression)
-- ========================================

WITH UserOrders AS (
    SELECT UserId, COUNT(*) as OrderCount
    FROM Orders
    GROUP BY UserId
)
SELECT u.Name, uo.OrderCount
FROM Users u
INNER JOIN UserOrders uo ON u.Id = uo.UserId;

-- ========================================
-- Window Functions
-- ========================================

SELECT 
    Name,
    Salary,
    ROW_NUMBER() OVER (ORDER BY Salary DESC) as RowNum,
    RANK() OVER (ORDER BY Salary DESC) as Rank,
    DENSE_RANK() OVER (ORDER BY Salary DESC) as DenseRank,
    LAG(Salary) OVER (ORDER BY Salary) as PrevSalary,
    LEAD(Salary) OVER (ORDER BY Salary) as NextSalary
FROM Employees;

-- ========================================
-- Temporal Tables
-- ========================================

CREATE TABLE Employee
(
    EmployeeId INT PRIMARY KEY,
    Name NVARCHAR(100),
    Position NVARCHAR(100),
    Department NVARCHAR(100),
    ValidFrom DATETIME2 GENERATED ALWAYS AS ROW START NOT NULL,
    ValidTo DATETIME2 GENERATED ALWAYS AS ROW END NOT NULL,
    PERIOD FOR SYSTEM_TIME (ValidFrom, ValidTo)
)
WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = dbo.EmployeeHistory));

-- ========================================
-- Graph Tables (SQL Server 2017+)
-- ========================================

CREATE TABLE Person (
    ID INTEGER PRIMARY KEY,
    Name VARCHAR(100)
) AS NODE;

CREATE TABLE Friends (
    FriendshipDate DATE
) AS EDGE;

-- ========================================
-- Backup/Restore
-- ========================================

BACKUP DATABASE MyDatabase 
TO DISK = 'C:\Backup\MyDatabase.bak'
WITH COMPRESSION, INIT;

RESTORE DATABASE MyDatabase
FROM DISK = 'C:\Backup\MyDatabase.bak'
WITH REPLACE, RECOVERY;

-- ========================================
-- Security
-- ========================================

CREATE LOGIN TestUser WITH PASSWORD = 'StrongPassword123!';

CREATE USER TestUser FOR LOGIN TestUser;

GRANT SELECT, INSERT, UPDATE ON Users TO TestUser;

REVOKE DELETE ON Users FROM TestUser;

-- ========================================
-- Transactions
-- ========================================

BEGIN TRANSACTION;

UPDATE Accounts SET Balance = Balance - 100 WHERE AccountId = 1;
UPDATE Accounts SET Balance = Balance + 100 WHERE AccountId = 2;

COMMIT TRANSACTION;

-- Com ROLLBACK
BEGIN TRANSACTION;

DELETE FROM Orders WHERE OrderDate < '2020-01-01';

ROLLBACK TRANSACTION;

-- ========================================
-- Dynamic SQL
-- ========================================

DECLARE @SQL NVARCHAR(MAX) = N'SELECT * FROM Users WHERE Id = @UserId';
DECLARE @Params NVARCHAR(MAX) = N'@UserId INT';

EXEC sp_executesql @SQL, @Params, @UserId = 1;

-- ========================================
-- Error Handling
-- ========================================

BEGIN TRY
    -- Código que pode gerar erro
    SELECT 1/0;
END TRY
BEGIN CATCH
    SELECT 
        ERROR_NUMBER() AS ErrorNumber,
        ERROR_MESSAGE() AS ErrorMessage,
        ERROR_SEVERITY() AS ErrorSeverity,
        ERROR_STATE() AS ErrorState;
END CATCH;
