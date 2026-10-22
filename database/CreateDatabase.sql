-- Chạy bằng SSMS 20 với tài khoản có quyền CREATE DATABASE.
-- Sau đó mở schema.sql, chọn database dthieu_0643 trên thanh công cụ và Execute.
USE [master];
GO
IF DB_ID(N'dthieu_0643') IS NULL
BEGIN
    CREATE DATABASE [dthieu_0643];
END;
GO
USE [dthieu_0643];
GO
