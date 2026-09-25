IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
CREATE TABLE [AspNetRoles] (
    [Id] nvarchar(450) NOT NULL,
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
);

CREATE TABLE [AspNetUsers] (
    [Id] nvarchar(450) NOT NULL,
    [LastName] nvarchar(max) NOT NULL,
    [FirstName] nvarchar(max) NOT NULL,
    [Patronymic] nvarchar(max) NULL,
    [RegistrationDate] datetime2 NOT NULL,
    [Balance] decimal(18,2) NOT NULL,
    [Status] int NOT NULL,
    [IsDeleted] bit NOT NULL,
    [UserName] nvarchar(256) NULL,
    [NormalizedUserName] nvarchar(256) NULL,
    [Email] nvarchar(256) NULL,
    [NormalizedEmail] nvarchar(256) NULL,
    [EmailConfirmed] bit NOT NULL,
    [PasswordHash] nvarchar(max) NULL,
    [SecurityStamp] nvarchar(max) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [PhoneNumberConfirmed] bit NOT NULL,
    [TwoFactorEnabled] bit NOT NULL,
    [LockoutEnd] datetimeoffset NULL,
    [LockoutEnabled] bit NOT NULL,
    [AccessFailedCount] int NOT NULL,
    CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
);

CREATE TABLE [SportEvents] (
    [Id] int NOT NULL IDENTITY,
    [SportType] nvarchar(100) NOT NULL,
    [Title] nvarchar(500) NOT NULL,
    [StartDate] datetime2 NOT NULL,
    [Status] int NOT NULL,
    [Result] nvarchar(max) NULL,
    [WinningCoefficientId] int NULL,
    [IsDeleted] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_SportEvents] PRIMARY KEY ([Id])
);

CREATE TABLE [AspNetRoleClaims] (
    [Id] int NOT NULL IDENTITY,
    [RoleId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [ActionLogs] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NULL,
    [Action] nvarchar(max) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [IpAddress] nvarchar(max) NULL,
    CONSTRAINT [PK_ActionLogs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ActionLogs_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id])
);

CREATE TABLE [AspNetUserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserLogins] (
    [LoginProvider] nvarchar(450) NOT NULL,
    [ProviderKey] nvarchar(450) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserRoles] (
    [UserId] nvarchar(450) NOT NULL,
    [RoleId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserTokens] (
    [UserId] nvarchar(450) NOT NULL,
    [LoginProvider] nvarchar(450) NOT NULL,
    [Name] nvarchar(450) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Bonuses] (
    [Id] int NOT NULL IDENTITY,
    [PlayerId] nvarchar(450) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [StartDate] datetime2 NOT NULL,
    [EndDate] datetime2 NOT NULL,
    [Status] int NOT NULL,
    CONSTRAINT [PK_Bonuses] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Bonuses_AspNetUsers_PlayerId] FOREIGN KEY ([PlayerId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Notifications] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [Type] int NOT NULL,
    [Text] nvarchar(max) NOT NULL,
    [SentAt] datetime2 NOT NULL,
    [Status] int NOT NULL,
    CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Notifications_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Coefficients] (
    [Id] int NOT NULL IDENTITY,
    [SportEventId] int NOT NULL,
    [OutcomeDescription] nvarchar(200) NOT NULL,
    [Value] decimal(10,2) NOT NULL,
    CONSTRAINT [PK_Coefficients] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Coefficients_SportEvents_SportEventId] FOREIGN KEY ([SportEventId]) REFERENCES [SportEvents] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Bets] (
    [Id] int NOT NULL IDENTITY,
    [PlayerId] nvarchar(450) NOT NULL,
    [SportEventId] int NOT NULL,
    [CoefficientId] int NOT NULL,
    [CoefficientValue] decimal(10,2) NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [Status] int NOT NULL,
    [Winnings] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_Bets] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Bets_AspNetUsers_PlayerId] FOREIGN KEY ([PlayerId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Bets_Coefficients_CoefficientId] FOREIGN KEY ([CoefficientId]) REFERENCES [Coefficients] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Bets_SportEvents_SportEventId] FOREIGN KEY ([SportEventId]) REFERENCES [SportEvents] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [AccountOperations] (
    [Id] int NOT NULL IDENTITY,
    [PlayerId] nvarchar(450) NOT NULL,
    [OperationType] int NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [BetId] int NULL,
    CONSTRAINT [PK_AccountOperations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AccountOperations_AspNetUsers_PlayerId] FOREIGN KEY ([PlayerId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AccountOperations_Bets_BetId] FOREIGN KEY ([BetId]) REFERENCES [Bets] ([Id]) ON DELETE SET NULL
);

CREATE INDEX [IX_AccountOperations_BetId] ON [AccountOperations] ([BetId]);

CREATE INDEX [IX_AccountOperations_PlayerId] ON [AccountOperations] ([PlayerId]);

CREATE INDEX [IX_ActionLogs_UserId] ON [ActionLogs] ([UserId]);

CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);

CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;

CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);

CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);

CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);

CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);

CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;

CREATE INDEX [IX_Bets_CoefficientId] ON [Bets] ([CoefficientId]);

CREATE INDEX [IX_Bets_PlayerId] ON [Bets] ([PlayerId]);

CREATE INDEX [IX_Bets_SportEventId] ON [Bets] ([SportEventId]);

CREATE INDEX [IX_Bonuses_PlayerId] ON [Bonuses] ([PlayerId]);

CREATE INDEX [IX_Coefficients_SportEventId] ON [Coefficients] ([SportEventId]);

CREATE INDEX [IX_Notifications_UserId] ON [Notifications] ([UserId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260809123158_InitialCreate', N'9.0.4');

COMMIT;
GO

