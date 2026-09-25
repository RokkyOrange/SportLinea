-- Слой 1 (пока без смены приложения).
-- Клиентская БД остаётся SportLineaDb — там живут реальные таблицы.
-- Админская БД SportLineaAdmin — «окно» на пересечение + место под журналы АРМ.
-- Связь: синонимы SQL Server. Две базы в SSMS, одни и те же строки по игрокам/событиям/ставкам.
-- Текущий сайт SportLinea этот скрипт не переключает.

IF DB_ID(N'SportLineaAdmin') IS NULL
    CREATE DATABASE [SportLineaAdmin];
GO

USE [SportLineaAdmin];
GO

-- Пересекающиеся сущности: видны в обеих БД, хранятся в SportLineaDb.
IF OBJECT_ID(N'dbo.AspNetUsers', N'SN') IS NULL
    CREATE SYNONYM dbo.AspNetUsers FOR SportLineaDb.dbo.AspNetUsers;
IF OBJECT_ID(N'dbo.AspNetRoles', N'SN') IS NULL
    CREATE SYNONYM dbo.AspNetRoles FOR SportLineaDb.dbo.AspNetRoles;
IF OBJECT_ID(N'dbo.AspNetUserRoles', N'SN') IS NULL
    CREATE SYNONYM dbo.AspNetUserRoles FOR SportLineaDb.dbo.AspNetUserRoles;
IF OBJECT_ID(N'dbo.AspNetUserClaims', N'SN') IS NULL
    CREATE SYNONYM dbo.AspNetUserClaims FOR SportLineaDb.dbo.AspNetUserClaims;
IF OBJECT_ID(N'dbo.AspNetRoleClaims', N'SN') IS NULL
    CREATE SYNONYM dbo.AspNetRoleClaims FOR SportLineaDb.dbo.AspNetRoleClaims;
IF OBJECT_ID(N'dbo.AspNetUserLogins', N'SN') IS NULL
    CREATE SYNONYM dbo.AspNetUserLogins FOR SportLineaDb.dbo.AspNetUserLogins;
IF OBJECT_ID(N'dbo.AspNetUserTokens', N'SN') IS NULL
    CREATE SYNONYM dbo.AspNetUserTokens FOR SportLineaDb.dbo.AspNetUserTokens;

IF OBJECT_ID(N'dbo.SportEvents', N'SN') IS NULL
    CREATE SYNONYM dbo.SportEvents FOR SportLineaDb.dbo.SportEvents;
IF OBJECT_ID(N'dbo.Coefficients', N'SN') IS NULL
    CREATE SYNONYM dbo.Coefficients FOR SportLineaDb.dbo.Coefficients;
IF OBJECT_ID(N'dbo.Bets', N'SN') IS NULL
    CREATE SYNONYM dbo.Bets FOR SportLineaDb.dbo.Bets;
IF OBJECT_ID(N'dbo.AccountOperations', N'SN') IS NULL
    CREATE SYNONYM dbo.AccountOperations FOR SportLineaDb.dbo.AccountOperations;
IF OBJECT_ID(N'dbo.Bonuses', N'SN') IS NULL
    CREATE SYNONYM dbo.Bonuses FOR SportLineaDb.dbo.Bonuses;
IF OBJECT_ID(N'dbo.WithdrawalRequests', N'SN') IS NULL
    CREATE SYNONYM dbo.WithdrawalRequests FOR SportLineaDb.dbo.WithdrawalRequests;
IF OBJECT_ID(N'dbo.Notifications', N'SN') IS NULL
    CREATE SYNONYM dbo.Notifications FOR SportLineaDb.dbo.Notifications;
IF OBJECT_ID(N'dbo.ActionLogs', N'SN') IS NULL
    CREATE SYNONYM dbo.ActionLogs FOR SportLineaDb.dbo.ActionLogs;
IF OBJECT_ID(N'dbo.[__EFMigrationsHistory]', N'SN') IS NULL
    CREATE SYNONYM dbo.[__EFMigrationsHistory] FOR SportLineaDb.dbo.[__EFMigrationsHistory];
GO
