
-- --------------------------------------------------
-- Entity Designer DDL Script for SQL Server 2005, 2008, 2012 and Azure
-- --------------------------------------------------
-- Date Created: 09/29/2025 17:54:39
-- Generated from EDMX file: C:\Users\Pablo Silva\source\repos\GooseGame\GameServer\GameDatabase.edmx
-- --------------------------------------------------

SET QUOTED_IDENTIFIER OFF;
GO
USE [GooseGame];
GO
IF SCHEMA_ID(N'dbo') IS NULL EXECUTE(N'CREATE SCHEMA [dbo]');
GO

-- --------------------------------------------------
-- Dropping existing FOREIGN KEY constraints
-- --------------------------------------------------


-- --------------------------------------------------
-- Dropping existing tables
-- --------------------------------------------------


-- --------------------------------------------------
-- Creating all tables
-- --------------------------------------------------

-- Creating table 'BoardSet'
CREATE TABLE [dbo].[BoardSet] (
    [idBoard] int IDENTITY(1,1) NOT NULL,
    [Name] nvarchar(max)  NOT NULL,
    [Description] nvarchar(max)  NOT NULL,
    [TileCount] int  NOT NULL
);
GO

-- Creating table 'Accounts'
CREATE TABLE [dbo].[Accounts] (
    [IdAccount] int IDENTITY(1,1) NOT NULL,
    [Email] nvarchar(255)  NOT NULL,
    [PasswordHash] nvarchar(255)  NOT NULL,
    [RegisterDate] datetime  NOT NULL,
    [AccountStatus] int  NOT NULL
);
GO

-- Creating table 'Players'
CREATE TABLE [dbo].[Players] (
    [IdPlayer] int IDENTITY(1,1) NOT NULL,
    [Username] nvarchar(max)  NOT NULL,
    [Coins] int  NOT NULL,
    [Avatar] nvarchar(max)  NOT NULL,
    [PlayerInventoryIdPlayerInventory] int  NOT NULL,
    [Account_IdAccount] int  NOT NULL
);
GO

-- Creating table 'PlayerStats'
CREATE TABLE [dbo].[PlayerStats] (
    [IdPlayers] int IDENTITY(1,1) NOT NULL,
    [MatchesPlayed] int  NOT NULL,
    [MatchesWon] int  NOT NULL,
    [MatchesLost] int  NOT NULL,
    [LuckyBoxOpened] int  NOT NULL,
    [IdPlayer_IdPlayer] int  NOT NULL
);
GO

-- Creating table 'Friendships'
CREATE TABLE [dbo].[Friendships] (
    [IdFriendship] int IDENTITY(1,1) NOT NULL,
    [FriendshipStatus] int  NOT NULL,
    [RequestDate] datetime  NOT NULL
);
GO

-- Creating table 'Sanctions'
CREATE TABLE [dbo].[Sanctions] (
    [IdSanction] int IDENTITY(1,1) NOT NULL,
    [SanctionType] int  NOT NULL,
    [Reason] nvarchar(max)  NOT NULL,
    [StartDate] datetime  NOT NULL,
    [EndDate] datetime  NOT NULL,
    [Account_IdAccount] int  NOT NULL,
    [Game_IdGame] int  NOT NULL
);
GO

-- Creating table 'Games'
CREATE TABLE [dbo].[Games] (
    [IdGame] int IDENTITY(1,1) NOT NULL,
    [GameStatus] int  NOT NULL,
    [StartTime] datetime  NOT NULL,
    [EndTime] datetime  NOT NULL,
    [HostPlayerID] nvarchar(max)  NOT NULL,
    [ChatMessageIdChatMessage] int  NOT NULL,
    [Board_idBoard] int  NOT NULL
);
GO

-- Creating table 'Items'
CREATE TABLE [dbo].[Items] (
    [IdItem] int IDENTITY(1,1) NOT NULL,
    [Name] nvarchar(max)  NOT NULL,
    [Description] nvarchar(max)  NOT NULL,
    [ItemType] int  NOT NULL,
    [RarityType] int  NOT NULL,
    [Cost] smallint  NOT NULL,
    [PlayerInventoryIdPlayerInventory] int  NOT NULL
);
GO

-- Creating table 'PlayerInventories'
CREATE TABLE [dbo].[PlayerInventories] (
    [IdPlayerInventory] int IDENTITY(1,1) NOT NULL
);
GO

-- Creating table 'Tiles'
CREATE TABLE [dbo].[Tiles] (
    [IdTile] int IDENTITY(1,1) NOT NULL,
    [TileNumber] int  NOT NULL,
    [Board_idBoard] int  NOT NULL
);
GO

-- Creating table 'ChatMessages'
CREATE TABLE [dbo].[ChatMessages] (
    [IdChatMessage] int IDENTITY(1,1) NOT NULL,
    [MessageText] nvarchar(max)  NOT NULL,
    [Player_IdPlayer] int  NOT NULL
);
GO

-- Creating table 'MoveRecords'
CREATE TABLE [dbo].[MoveRecords] (
    [IdMoveRecord] int IDENTITY(1,1) NOT NULL,
    [TurnNumber] int  NOT NULL,
    [DiceOne] int  NOT NULL,
    [DiceTwo] int  NOT NULL,
    [StartPosition] int  NOT NULL,
    [FinalPosition] int  NOT NULL,
    [ActionDescription] nvarchar(max)  NOT NULL
);
GO

-- --------------------------------------------------
-- Creating all PRIMARY KEY constraints
-- --------------------------------------------------

-- Creating primary key on [idBoard] in table 'BoardSet'
ALTER TABLE [dbo].[BoardSet]
ADD CONSTRAINT [PK_BoardSet]
    PRIMARY KEY CLUSTERED ([idBoard] ASC);
GO

-- Creating primary key on [IdAccount] in table 'Accounts'
ALTER TABLE [dbo].[Accounts]
ADD CONSTRAINT [PK_Accounts]
    PRIMARY KEY CLUSTERED ([IdAccount] ASC);
GO

-- Creating primary key on [IdPlayer] in table 'Players'
ALTER TABLE [dbo].[Players]
ADD CONSTRAINT [PK_Players]
    PRIMARY KEY CLUSTERED ([IdPlayer] ASC);
GO

-- Creating primary key on [IdPlayers] in table 'PlayerStats'
ALTER TABLE [dbo].[PlayerStats]
ADD CONSTRAINT [PK_PlayerStats]
    PRIMARY KEY CLUSTERED ([IdPlayers] ASC);
GO

-- Creating primary key on [IdFriendship] in table 'Friendships'
ALTER TABLE [dbo].[Friendships]
ADD CONSTRAINT [PK_Friendships]
    PRIMARY KEY CLUSTERED ([IdFriendship] ASC);
GO

-- Creating primary key on [IdSanction] in table 'Sanctions'
ALTER TABLE [dbo].[Sanctions]
ADD CONSTRAINT [PK_Sanctions]
    PRIMARY KEY CLUSTERED ([IdSanction] ASC);
GO

-- Creating primary key on [IdGame] in table 'Games'
ALTER TABLE [dbo].[Games]
ADD CONSTRAINT [PK_Games]
    PRIMARY KEY CLUSTERED ([IdGame] ASC);
GO

-- Creating primary key on [IdItem] in table 'Items'
ALTER TABLE [dbo].[Items]
ADD CONSTRAINT [PK_Items]
    PRIMARY KEY CLUSTERED ([IdItem] ASC);
GO

-- Creating primary key on [IdPlayerInventory] in table 'PlayerInventories'
ALTER TABLE [dbo].[PlayerInventories]
ADD CONSTRAINT [PK_PlayerInventories]
    PRIMARY KEY CLUSTERED ([IdPlayerInventory] ASC);
GO

-- Creating primary key on [IdTile] in table 'Tiles'
ALTER TABLE [dbo].[Tiles]
ADD CONSTRAINT [PK_Tiles]
    PRIMARY KEY CLUSTERED ([IdTile] ASC);
GO

-- Creating primary key on [IdChatMessage] in table 'ChatMessages'
ALTER TABLE [dbo].[ChatMessages]
ADD CONSTRAINT [PK_ChatMessages]
    PRIMARY KEY CLUSTERED ([IdChatMessage] ASC);
GO

-- Creating primary key on [IdMoveRecord] in table 'MoveRecords'
ALTER TABLE [dbo].[MoveRecords]
ADD CONSTRAINT [PK_MoveRecords]
    PRIMARY KEY CLUSTERED ([IdMoveRecord] ASC);
GO

-- --------------------------------------------------
-- Creating all FOREIGN KEY constraints
-- --------------------------------------------------

-- Creating foreign key on [Account_IdAccount] in table 'Players'
ALTER TABLE [dbo].[Players]
ADD CONSTRAINT [FK_PlayerAccount]
    FOREIGN KEY ([Account_IdAccount])
    REFERENCES [dbo].[Accounts]
        ([IdAccount])
    ON DELETE NO ACTION ON UPDATE NO ACTION;
GO

-- Creating non-clustered index for FOREIGN KEY 'FK_PlayerAccount'
CREATE INDEX [IX_FK_PlayerAccount]
ON [dbo].[Players]
    ([Account_IdAccount]);
GO

-- Creating foreign key on [IdPlayer_IdPlayer] in table 'PlayerStats'
ALTER TABLE [dbo].[PlayerStats]
ADD CONSTRAINT [FK_PlayerStatPlayer]
    FOREIGN KEY ([IdPlayer_IdPlayer])
    REFERENCES [dbo].[Players]
        ([IdPlayer])
    ON DELETE NO ACTION ON UPDATE NO ACTION;
GO

-- Creating non-clustered index for FOREIGN KEY 'FK_PlayerStatPlayer'
CREATE INDEX [IX_FK_PlayerStatPlayer]
ON [dbo].[PlayerStats]
    ([IdPlayer_IdPlayer]);
GO

-- Creating foreign key on [Account_IdAccount] in table 'Sanctions'
ALTER TABLE [dbo].[Sanctions]
ADD CONSTRAINT [FK_IdAccount]
    FOREIGN KEY ([Account_IdAccount])
    REFERENCES [dbo].[Accounts]
        ([IdAccount])
    ON DELETE NO ACTION ON UPDATE NO ACTION;
GO

-- Creating non-clustered index for FOREIGN KEY 'FK_IdAccount'
CREATE INDEX [IX_FK_IdAccount]
ON [dbo].[Sanctions]
    ([Account_IdAccount]);
GO

-- Creating foreign key on [Game_IdGame] in table 'Sanctions'
ALTER TABLE [dbo].[Sanctions]
ADD CONSTRAINT [FK_idGame]
    FOREIGN KEY ([Game_IdGame])
    REFERENCES [dbo].[Games]
        ([IdGame])
    ON DELETE NO ACTION ON UPDATE NO ACTION;
GO

-- Creating non-clustered index for FOREIGN KEY 'FK_idGame'
CREATE INDEX [IX_FK_idGame]
ON [dbo].[Sanctions]
    ([Game_IdGame]);
GO

-- Creating foreign key on [PlayerInventoryIdPlayerInventory] in table 'Players'
ALTER TABLE [dbo].[Players]
ADD CONSTRAINT [FK_idPlayer]
    FOREIGN KEY ([PlayerInventoryIdPlayerInventory])
    REFERENCES [dbo].[PlayerInventories]
        ([IdPlayerInventory])
    ON DELETE NO ACTION ON UPDATE NO ACTION;
GO

-- Creating non-clustered index for FOREIGN KEY 'FK_idPlayer'
CREATE INDEX [IX_FK_idPlayer]
ON [dbo].[Players]
    ([PlayerInventoryIdPlayerInventory]);
GO

-- Creating foreign key on [PlayerInventoryIdPlayerInventory] in table 'Items'
ALTER TABLE [dbo].[Items]
ADD CONSTRAINT [FK_idItem]
    FOREIGN KEY ([PlayerInventoryIdPlayerInventory])
    REFERENCES [dbo].[PlayerInventories]
        ([IdPlayerInventory])
    ON DELETE NO ACTION ON UPDATE NO ACTION;
GO

-- Creating non-clustered index for FOREIGN KEY 'FK_idItem'
CREATE INDEX [IX_FK_idItem]
ON [dbo].[Items]
    ([PlayerInventoryIdPlayerInventory]);
GO

-- Creating foreign key on [Board_idBoard] in table 'Tiles'
ALTER TABLE [dbo].[Tiles]
ADD CONSTRAINT [FK_idBoard]
    FOREIGN KEY ([Board_idBoard])
    REFERENCES [dbo].[BoardSet]
        ([idBoard])
    ON DELETE NO ACTION ON UPDATE NO ACTION;
GO

-- Creating non-clustered index for FOREIGN KEY 'FK_idBoard'
CREATE INDEX [IX_FK_idBoard]
ON [dbo].[Tiles]
    ([Board_idBoard]);
GO

-- Creating foreign key on [Board_idBoard] in table 'Games'
ALTER TABLE [dbo].[Games]
ADD CONSTRAINT [FK_GameBoard]
    FOREIGN KEY ([Board_idBoard])
    REFERENCES [dbo].[BoardSet]
        ([idBoard])
    ON DELETE NO ACTION ON UPDATE NO ACTION;
GO

-- Creating non-clustered index for FOREIGN KEY 'FK_GameBoard'
CREATE INDEX [IX_FK_GameBoard]
ON [dbo].[Games]
    ([Board_idBoard]);
GO

-- Creating foreign key on [Player_IdPlayer] in table 'ChatMessages'
ALTER TABLE [dbo].[ChatMessages]
ADD CONSTRAINT [FK_ChatMessagePlayer]
    FOREIGN KEY ([Player_IdPlayer])
    REFERENCES [dbo].[Players]
        ([IdPlayer])
    ON DELETE NO ACTION ON UPDATE NO ACTION;
GO

-- Creating non-clustered index for FOREIGN KEY 'FK_ChatMessagePlayer'
CREATE INDEX [IX_FK_ChatMessagePlayer]
ON [dbo].[ChatMessages]
    ([Player_IdPlayer]);
GO

-- Creating foreign key on [ChatMessageIdChatMessage] in table 'Games'
ALTER TABLE [dbo].[Games]
ADD CONSTRAINT [FK_ChatMessageGame]
    FOREIGN KEY ([ChatMessageIdChatMessage])
    REFERENCES [dbo].[ChatMessages]
        ([IdChatMessage])
    ON DELETE NO ACTION ON UPDATE NO ACTION;
GO

-- Creating non-clustered index for FOREIGN KEY 'FK_ChatMessageGame'
CREATE INDEX [IX_FK_ChatMessageGame]
ON [dbo].[Games]
    ([ChatMessageIdChatMessage]);
GO

-- --------------------------------------------------
-- Script has ended
-- --------------------------------------------------