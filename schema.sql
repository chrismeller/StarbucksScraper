USE [Stores]
GO
/****** Object:  Table [dbo].[Features]    Script Date: 11/10/2013 4:50:04 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Features](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[StoreID] [int] NOT NULL,
	[Code] [nvarchar](5) NOT NULL,
	[Name] [nvarchar](255) NOT NULL,
 CONSTRAINT [PK_Features] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY],
 CONSTRAINT [UK_Features_StoreID_Code] UNIQUE NONCLUSTERED 
(
	[StoreID] ASC,
	[Code] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
/****** Object:  Table [dbo].[RegularHours]    Script Date: 11/10/2013 4:50:04 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[RegularHours](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[StoreID] [int] NOT NULL,
	[Day] [nvarchar](10) NOT NULL,
	[Open] [bit] NOT NULL,
	[Open24Hours] [bit] NOT NULL,
	[OpenTime] [nvarchar](8) NOT NULL,
	[CloseTime] [nvarchar](8) NOT NULL,
 CONSTRAINT [PK_RegularHours] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY],
 CONSTRAINT [UK_RegularHours_StoreID_Day] UNIQUE NONCLUSTERED 
(
	[StoreID] ASC,
	[Day] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
/****** Object:  Table [dbo].[Stores]    Script Date: 11/10/2013 4:50:04 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Stores](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[StarbucksStoreID] [int] NOT NULL,
	[Name] [nvarchar](255) NOT NULL,
	[BrandName] [nvarchar](50) NOT NULL,
	[StoreNumber] [nvarchar](15) NOT NULL,
	[PhoneNumber] [nvarchar](20) NULL,
	[OwnershipType] [nvarchar](5) NOT NULL,
	[Street1] [nvarchar](255) NULL,
	[Street2] [nvarchar](255) NULL,
	[Street3] [nvarchar](255) NULL,
	[City] [nvarchar](255) NULL,
	[CountrySubdivisionCode] [nvarchar](3) NOT NULL,
	[CountryCode] [nvarchar](2) NOT NULL,
	[PostalCode] [nvarchar](15) NULL,
	[Latitude] [float] NULL,
	[Longitude] [float] NULL,
	[TZOffset] [int] NOT NULL,
	[TZID] [nvarchar](50) NULL,
	[TZOlsonID] [nvarchar](50) NOT NULL,
 CONSTRAINT [PK_Stores] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY],
 CONSTRAINT [UK_Stores_StarbucksStoreID] UNIQUE NONCLUSTERED 
(
	[StarbucksStoreID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
ALTER TABLE [dbo].[Features]  WITH CHECK ADD  CONSTRAINT [FK_Features_Stores] FOREIGN KEY([StoreID])
REFERENCES [dbo].[Stores] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[Features] CHECK CONSTRAINT [FK_Features_Stores]
GO
ALTER TABLE [dbo].[RegularHours]  WITH CHECK ADD  CONSTRAINT [FK_RegularHours_Stores] FOREIGN KEY([StoreID])
REFERENCES [dbo].[Stores] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[RegularHours] CHECK CONSTRAINT [FK_RegularHours_Stores]
GO
