CREATE TABLE [Accounts] (
    [Id] uniqueidentifier NOT NULL,
    [UserName] nvarchar(40) NOT NULL,
    [Email] nvarchar(200) NOT NULL,
    [FullName] nvarchar(100) NOT NULL,
    [PasswordHash] nvarchar(max) NOT NULL,
    [Role] nvarchar(max) NOT NULL,
    [Active] bit NOT NULL,
    [EmailConfirmed] bit NOT NULL,
    [FailedLogins] int NOT NULL,
    [LockedUntilUtc] datetime2 NULL,
    [FacePersonId] nvarchar(max) NULL,
    CONSTRAINT [PK_Accounts] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [AccessSessions] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [TokenHash] nvarchar(64) NOT NULL,
    [ExpiresUtc] datetime2 NOT NULL,
    [AuthenticatedUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_AccessSessions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AccessSessions_Accounts_UserId] FOREIGN KEY ([UserId]) REFERENCES [Accounts] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [Challenges] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [Purpose] nvarchar(max) NOT NULL,
    [CodeHash] nvarchar(max) NOT NULL,
    [ExpiresUtc] datetime2 NOT NULL,
    [Attempts] int NOT NULL,
    [Used] bit NOT NULL,
    [Version] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Challenges] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Challenges_Accounts_UserId] FOREIGN KEY ([UserId]) REFERENCES [Accounts] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [Courses] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(30) NOT NULL,
    [Name] nvarchar(150) NOT NULL,
    [LecturerId] uniqueidentifier NOT NULL,
    [PlannedSessions] int NOT NULL,
    [RequiredPercent] int NOT NULL,
    CONSTRAINT [PK_Courses] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Courses_Accounts_LecturerId] FOREIGN KEY ([LecturerId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [ExternalIdentities] (
    [Provider] nvarchar(30) NOT NULL,
    [Subject] nvarchar(255) NOT NULL,
    [AccountId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_ExternalIdentities] PRIMARY KEY ([Provider], [Subject]),
    CONSTRAINT [FK_ExternalIdentities_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [Enrollments] (
    [CourseId] uniqueidentifier NOT NULL,
    [StudentId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Enrollments] PRIMARY KEY ([CourseId], [StudentId]),
    CONSTRAINT [FK_Enrollments_Accounts_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Enrollments_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [Sessions] (
    [Id] uniqueidentifier NOT NULL,
    [CourseId] uniqueidentifier NOT NULL,
    [StartsUtc] datetime2 NOT NULL,
    [EndsUtc] datetime2 NOT NULL,
    [Latitude] float NOT NULL,
    [Longitude] float NOT NULL,
    [RadiusMeters] int NOT NULL,
    [LateAfterMinutes] int NOT NULL,
    [Closed] bit NOT NULL,
    [Version] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Sessions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Sessions_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [Attendances] (
    [Id] uniqueidentifier NOT NULL,
    [SessionId] uniqueidentifier NOT NULL,
    [StudentId] uniqueidentifier NOT NULL,
    [CheckedInUtc] datetime2 NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [DistanceMeters] float NOT NULL,
    [Latitude] float NOT NULL,
    [Longitude] float NOT NULL,
    CONSTRAINT [PK_Attendances] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Attendances_Accounts_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Attendances_Sessions_SessionId] FOREIGN KEY ([SessionId]) REFERENCES [Sessions] ([Id]) ON DELETE CASCADE
);
GO


CREATE UNIQUE INDEX [IX_AccessSessions_TokenHash] ON [AccessSessions] ([TokenHash]);
GO


CREATE INDEX [IX_AccessSessions_UserId] ON [AccessSessions] ([UserId]);
GO


CREATE UNIQUE INDEX [IX_Accounts_Email] ON [Accounts] ([Email]);
GO


CREATE UNIQUE INDEX [IX_Accounts_UserName] ON [Accounts] ([UserName]);
GO


CREATE UNIQUE INDEX [IX_Attendances_SessionId_StudentId] ON [Attendances] ([SessionId], [StudentId]);
GO


CREATE INDEX [IX_Attendances_StudentId] ON [Attendances] ([StudentId]);
GO


CREATE INDEX [IX_Challenges_UserId] ON [Challenges] ([UserId]);
GO


CREATE UNIQUE INDEX [IX_Courses_Code] ON [Courses] ([Code]);
GO


CREATE INDEX [IX_Courses_LecturerId] ON [Courses] ([LecturerId]);
GO


CREATE INDEX [IX_Enrollments_StudentId] ON [Enrollments] ([StudentId]);
GO


CREATE UNIQUE INDEX [IX_ExternalIdentities_AccountId_Provider] ON [ExternalIdentities] ([AccountId], [Provider]);
GO


CREATE INDEX [IX_Sessions_CourseId] ON [Sessions] ([CourseId]);
GO



