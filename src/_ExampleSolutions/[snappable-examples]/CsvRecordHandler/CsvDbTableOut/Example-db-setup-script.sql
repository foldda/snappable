/****** 
Table: STAGING_1

******/

USE Test
GO

IF OBJECT_ID('[sp_PRE_PROC]', 'P') IS NOT NULL
DROP PROC [dbo].[sp_PRE_PROC]
GO

CREATE procedure [dbo].[sp_PRE_PROC] 
	@param1 VARCHAR(255) = NULL  --- place-holder eg for dynamic sql
AS

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[STAGING_1]') AND type in (N'U'))
BEGIN
	CREATE TABLE [dbo].[STAGING_1]
	(
		[LAST_NAME] VARCHAR(255) NULL,
		[FIRST_NAME] VARCHAR(255) NULL,
		[ADDRESS] VARCHAR(255) NULL,
		[SUBURB] VARCHAR(255) NULL,
		[STATE] VARCHAR(255) NULL,
		[ZIP] VARCHAR(255) NULL,

	) ON [PRIMARY]
END;
ELSE
BEGIN
	TRUNCATE TABLE [dbo].[STAGING_1]
	-- DROP TABLE [dbo].[STAGING_1] 
END;

GO


-------------------

SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

IF OBJECT_ID('[sp_POST_PROC]', 'P') IS NOT NULL
DROP PROC [dbo].[sp_POST_PROC]
GO

CREATE procedure [dbo].[sp_POST_PROC] 
	@param1 VARCHAR(255) = NULL 
AS

/*
nothing

select top 100 * from dbo.[STAGING_1]
*/

GO
