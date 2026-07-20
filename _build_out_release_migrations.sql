CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260509152018_InitialCreate') THEN
    CREATE EXTENSION IF NOT EXISTS vector;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260509152018_InitialCreate') THEN
    CREATE TABLE chat_sessions (
        "Id" uuid NOT NULL,
        "TableCode" character varying(50) NOT NULL,
        "StartedAtUtc" timestamp with time zone NOT NULL,
        "EndedAtUtc" timestamp with time zone,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_chat_sessions" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260509152018_InitialCreate') THEN
    CREATE TABLE menu_categories (
        "Id" uuid NOT NULL,
        "Name" character varying(200) NOT NULL,
        "SortOrder" integer NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_menu_categories" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260509152018_InitialCreate') THEN
    CREATE TABLE orders (
        "Id" uuid NOT NULL,
        "TableCode" character varying(50) NOT NULL,
        "Status" integer NOT NULL,
        "TotalAmount" numeric(10,2) NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_orders" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260509152018_InitialCreate') THEN
    CREATE TABLE chat_messages (
        "Id" uuid NOT NULL,
        "ChatSessionId" uuid NOT NULL,
        "Role" integer NOT NULL,
        "Content" character varying(8000) NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_chat_messages" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_chat_messages_chat_sessions_ChatSessionId" FOREIGN KEY ("ChatSessionId") REFERENCES chat_sessions ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260509152018_InitialCreate') THEN
    CREATE TABLE menu_items (
        "Id" uuid NOT NULL,
        "CategoryId" uuid NOT NULL,
        "Name" character varying(200) NOT NULL,
        "Description" character varying(2000),
        "Price" numeric(10,2) NOT NULL,
        "IsAvailable" boolean NOT NULL,
        "ImageUrl" character varying(2000),
        "Embedding" vector,
        "EmbeddingModel" character varying(200),
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_menu_items" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_menu_items_menu_categories_CategoryId" FOREIGN KEY ("CategoryId") REFERENCES menu_categories ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260509152018_InitialCreate') THEN
    CREATE TABLE order_items (
        "Id" uuid NOT NULL,
        "OrderId" uuid NOT NULL,
        "MenuItemId" uuid NOT NULL,
        "Quantity" integer NOT NULL,
        "UnitPrice" numeric(10,2) NOT NULL,
        "Notes" character varying(1000),
        CONSTRAINT "PK_order_items" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_order_items_menu_items_MenuItemId" FOREIGN KEY ("MenuItemId") REFERENCES menu_items ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_order_items_orders_OrderId" FOREIGN KEY ("OrderId") REFERENCES orders ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260509152018_InitialCreate') THEN
    CREATE INDEX "IX_chat_messages_ChatSessionId" ON chat_messages ("ChatSessionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260509152018_InitialCreate') THEN
    CREATE INDEX "IX_chat_sessions_TableCode" ON chat_sessions ("TableCode");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260509152018_InitialCreate') THEN
    CREATE INDEX "IX_menu_items_CategoryId" ON menu_items ("CategoryId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260509152018_InitialCreate') THEN
    CREATE INDEX "IX_order_items_MenuItemId" ON order_items ("MenuItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260509152018_InitialCreate') THEN
    CREATE INDEX "IX_order_items_OrderId" ON order_items ("OrderId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260509152018_InitialCreate') THEN
    CREATE INDEX "IX_orders_TableCode" ON orders ("TableCode");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260509152018_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260509152018_InitialCreate', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510050332_AddMenuItemEditorialFields') THEN
    ALTER TABLE menu_items ADD "MoodProfile" character varying(160);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510050332_AddMenuItemEditorialFields') THEN
    ALTER TABLE menu_items ADD "ShortStory" character varying(1200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510050332_AddMenuItemEditorialFields') THEN
    ALTER TABLE menu_items ADD "TastingNotes" character varying(800);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510050332_AddMenuItemEditorialFields') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260510050332_AddMenuItemEditorialFields', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510051444_AddMenuItemSensoryAndIngredients') THEN
    ALTER TABLE menu_items ADD "AcidityLevel" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510051444_AddMenuItemSensoryAndIngredients') THEN
    ALTER TABLE menu_items ADD "CaffeineLevel" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510051444_AddMenuItemSensoryAndIngredients') THEN
    ALTER TABLE menu_items ADD "IngredientBreakdown" character varying(2000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510051444_AddMenuItemSensoryAndIngredients') THEN
    ALTER TABLE menu_items ADD "SweetnessLevel" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510051444_AddMenuItemSensoryAndIngredients') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260510051444_AddMenuItemSensoryAndIngredients', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510053743_AddOrderStatusFinishingTouchesEnum') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260510053743_AddOrderStatusFinishingTouchesEnum', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510065458_AddMenuItemSensoryProfileJson') THEN
    ALTER TABLE menu_items ADD "SensoryProfile" jsonb;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510065458_AddMenuItemSensoryProfileJson') THEN
    UPDATE "menu_items" SET "SensoryProfile" = '{}'::jsonb WHERE "SensoryProfile" IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510065458_AddMenuItemSensoryProfileJson') THEN
    UPDATE "menu_items" SET "EmbeddingModel" = NULL WHERE "Embedding" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510065458_AddMenuItemSensoryProfileJson') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260510065458_AddMenuItemSensoryProfileJson', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510081542_VenueTablesAndGuestOrderTokens') THEN
    ALTER TABLE orders ADD "GuestSessionToken" character varying(80);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510081542_VenueTablesAndGuestOrderTokens') THEN
    ALTER TABLE orders ADD "VenueTableId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510081542_VenueTablesAndGuestOrderTokens') THEN
    CREATE TABLE venue_tables (
        "Id" uuid NOT NULL,
        "VenueCode" character varying(32) NOT NULL,
        "DisplayCode" character varying(40) NOT NULL,
        "PublicSlug" character varying(80) NOT NULL,
        "DisplayLabel" character varying(120),
        "IsActive" boolean NOT NULL,
        CONSTRAINT "PK_venue_tables" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510081542_VenueTablesAndGuestOrderTokens') THEN
    CREATE UNIQUE INDEX "IX_orders_GuestSessionToken" ON orders ("GuestSessionToken");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510081542_VenueTablesAndGuestOrderTokens') THEN
    CREATE INDEX "IX_orders_VenueTableId" ON orders ("VenueTableId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510081542_VenueTablesAndGuestOrderTokens') THEN
    CREATE UNIQUE INDEX "IX_venue_tables_PublicSlug" ON venue_tables ("PublicSlug");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510081542_VenueTablesAndGuestOrderTokens') THEN
    CREATE UNIQUE INDEX "IX_venue_tables_VenueCode_DisplayCode" ON venue_tables ("VenueCode", "DisplayCode");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510081542_VenueTablesAndGuestOrderTokens') THEN
    ALTER TABLE orders ADD CONSTRAINT "FK_orders_venue_tables_VenueTableId" FOREIGN KEY ("VenueTableId") REFERENCES venue_tables ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510081542_VenueTablesAndGuestOrderTokens') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260510081542_VenueTablesAndGuestOrderTokens', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510210000_AddMenuItemIsArchived') THEN
    ALTER TABLE menu_items ADD "IsArchived" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510210000_AddMenuItemIsArchived') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260510210000_AddMenuItemIsArchived', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510233000_HospitalityReliabilityOps') THEN
    CREATE TABLE ingredients (
        "Id" uuid NOT NULL,
        "Name" character varying(160) NOT NULL,
        "Unit" character varying(40) NOT NULL,
        "CurrentStock" numeric(14,4) NOT NULL,
        "LowStockThreshold" numeric(14,4) NOT NULL,
        "IsActive" boolean NOT NULL DEFAULT TRUE,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_ingredients" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510233000_HospitalityReliabilityOps') THEN
    CREATE TABLE menu_item_ingredients (
        "Id" uuid NOT NULL,
        "MenuItemId" uuid NOT NULL,
        "IngredientId" uuid NOT NULL,
        "QuantityRequired" numeric(14,4) NOT NULL,
        CONSTRAINT "PK_menu_item_ingredients" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_menu_item_ingredients_menu_items_MenuItemId" FOREIGN KEY ("MenuItemId") REFERENCES menu_items ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_menu_item_ingredients_ingredients_IngredientId" FOREIGN KEY ("IngredientId") REFERENCES ingredients ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510233000_HospitalityReliabilityOps') THEN
    CREATE UNIQUE INDEX "IX_menu_item_ingredients_MenuItemId_IngredientId" ON menu_item_ingredients ("MenuItemId", "IngredientId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510233000_HospitalityReliabilityOps') THEN
    CREATE INDEX "IX_menu_item_ingredients_IngredientId" ON menu_item_ingredients ("IngredientId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510233000_HospitalityReliabilityOps') THEN
    CREATE TABLE sommelier_suggestion_feedback (
        "Id" uuid NOT NULL,
        "SessionId" uuid NOT NULL,
        "MenuItemId" uuid NOT NULL,
        "Outcome" character varying(32) NOT NULL,
        "MoodKey" character varying(64),
        "RefinementKey" character varying(64),
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_sommelier_suggestion_feedback" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510233000_HospitalityReliabilityOps') THEN
    CREATE INDEX "IX_sommelier_suggestion_feedback_SessionId" ON sommelier_suggestion_feedback ("SessionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510233000_HospitalityReliabilityOps') THEN
    CREATE INDEX "IX_sommelier_suggestion_feedback_MenuItemId" ON sommelier_suggestion_feedback ("MenuItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510233000_HospitalityReliabilityOps') THEN
    ALTER TABLE orders ADD "BrewingOwnerStaffName" character varying(120);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510233000_HospitalityReliabilityOps') THEN
    ALTER TABLE orders ADD "ServingOwnerStaffName" character varying(120);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510233000_HospitalityReliabilityOps') THEN
    ALTER TABLE orders ADD "StatusChangedAtUtc" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510233000_HospitalityReliabilityOps') THEN
    UPDATE "orders" SET "StatusChangedAtUtc" = COALESCE("UpdatedAtUtc", "CreatedAtUtc")
    WHERE "StatusChangedAtUtc" IS NULL
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510233000_HospitalityReliabilityOps') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260510233000_HospitalityReliabilityOps', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260511184452_AddMenuCurationFields') THEN
    ALTER TABLE menu_items ADD "IsFeatured" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260511184452_AddMenuCurationFields') THEN
    ALTER TABLE menu_items ADD "IsSeasonalHighlight" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260511184452_AddMenuCurationFields') THEN
    ALTER TABLE menu_items ADD "IsSignature" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260511184452_AddMenuCurationFields') THEN
    ALTER TABLE menu_items ADD "Subtitle" character varying(240);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260511184452_AddMenuCurationFields') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260511184452_AddMenuCurationFields', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260511194808_Phase10OrderSafetyAudit') THEN
    ALTER TABLE orders ADD "SubmitIdempotencyKey" character varying(120);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260511194808_Phase10OrderSafetyAudit') THEN
    CREATE TABLE operational_audit_entries (
        "Id" uuid NOT NULL,
        "OccurredAtUtc" timestamp with time zone NOT NULL,
        "ActionKind" character varying(96) NOT NULL,
        "Actor" character varying(160),
        "OrderId" uuid,
        "Summary" character varying(2000) NOT NULL,
        CONSTRAINT "PK_operational_audit_entries" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260511194808_Phase10OrderSafetyAudit') THEN
    CREATE UNIQUE INDEX "IX_orders_SubmitIdempotencyKey" ON orders ("SubmitIdempotencyKey") WHERE "SubmitIdempotencyKey" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260511194808_Phase10OrderSafetyAudit') THEN
    CREATE INDEX "IX_operational_audit_entries_OccurredAtUtc" ON operational_audit_entries ("OccurredAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260511194808_Phase10OrderSafetyAudit') THEN
    CREATE INDEX "IX_operational_audit_entries_OrderId" ON operational_audit_entries ("OrderId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260511194808_Phase10OrderSafetyAudit') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260511194808_Phase10OrderSafetyAudit', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512000000_ExperienceCurationCms') THEN
    ALTER TABLE menu_items ADD "DiscoveryWeight" numeric(10,4) NOT NULL DEFAULT 1.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512000000_ExperienceCurationCms') THEN
    ALTER TABLE menu_items ADD "IsHiddenDiscovery" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512000000_ExperienceCurationCms') THEN
    ALTER TABLE menu_items ADD "StoryCopy" character varying(2000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512000000_ExperienceCurationCms') THEN
    ALTER TABLE menu_items ADD "MoodTags" character varying(600);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512000000_ExperienceCurationCms') THEN
    ALTER TABLE menu_items ADD "FlavorTags" character varying(600);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512000000_ExperienceCurationCms') THEN
    UPDATE "menu_items" SET "DiscoveryWeight" = 0;

    UPDATE "menu_items" SET "DiscoveryWeight" = 1
    WHERE "IsArchived" = FALSE AND "IsAvailable" = TRUE
      AND ("IsSignature" = TRUE OR "IsFeatured" = TRUE OR "IsSeasonalHighlight" = TRUE);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512000000_ExperienceCurationCms') THEN
    CREATE TABLE experience_discovery_settings (
        "Id" uuid NOT NULL,
        "SeasonalOnlyPool" boolean NOT NULL DEFAULT FALSE,
        "CourierMoodCopy" character varying(1200),
        "FatigueCopyEvenLeg" character varying(600),
        "FatigueCopyOddLeg" character varying(600),
        "RerollPacingJson" character varying(8000),
        "RevealCopyNotes" character varying(2000),
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_experience_discovery_settings" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512000000_ExperienceCurationCms') THEN
    INSERT INTO "experience_discovery_settings" ("Id","SeasonalOnlyPool","CourierMoodCopy","FatigueCopyEvenLeg","FatigueCopyOddLeg","RerollPacingJson","RevealCopyNotes","CreatedAtUtc")
    SELECT 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1', FALSE,
        'The house sends a quiet courier — no hurry in the wings.',
        'Please respect the courier.',
        'The courier has carried enough for tonight.',
        '{}', NULL, TIMESTAMPTZ '2026-05-10T00:00:00Z'
    WHERE NOT EXISTS (SELECT 1 FROM "experience_discovery_settings" LIMIT 1);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512000000_ExperienceCurationCms') THEN
    CREATE TABLE experience_signature_slots (
        "Id" uuid NOT NULL,
        "MenuItemId" uuid NOT NULL,
        "SortOrder" integer NOT NULL,
        "IsSpotlight" boolean NOT NULL DEFAULT FALSE,
        "SeasonalSpotlightEnabled" boolean NOT NULL DEFAULT FALSE,
        "EditorialKicker" character varying(240),
        "EditorialBody" character varying(1200),
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_experience_signature_slots" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_experience_signature_slots_menu_items_MenuItemId" FOREIGN KEY ("MenuItemId") REFERENCES menu_items ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512000000_ExperienceCurationCms') THEN
    CREATE INDEX "IX_experience_signature_slots_MenuItemId" ON experience_signature_slots ("MenuItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512000000_ExperienceCurationCms') THEN
    CREATE INDEX "IX_experience_signature_slots_SortOrder" ON experience_signature_slots ("SortOrder");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512000000_ExperienceCurationCms') THEN
    CREATE TABLE experience_guided_questions (
        "Id" uuid NOT NULL,
        "ExternalKey" character varying(64) NOT NULL,
        "Prompt" character varying(600) NOT NULL,
        "SortOrder" integer NOT NULL,
        "IsOptional" boolean NOT NULL DEFAULT FALSE,
        "IsEnabled" boolean NOT NULL DEFAULT TRUE,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_experience_guided_questions" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512000000_ExperienceCurationCms') THEN
    CREATE UNIQUE INDEX "IX_experience_guided_questions_ExternalKey" ON experience_guided_questions ("ExternalKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512000000_ExperienceCurationCms') THEN
    CREATE INDEX "IX_experience_guided_questions_SortOrder" ON experience_guided_questions ("SortOrder");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512000000_ExperienceCurationCms') THEN
    CREATE TABLE experience_guided_options (
        "Id" uuid NOT NULL,
        "QuestionId" uuid NOT NULL,
        "ExternalKey" character varying(96) NOT NULL,
        "Label" character varying(200) NOT NULL,
        "Subline" character varying(400),
        "SortOrder" integer NOT NULL,
        "IsEnabled" boolean NOT NULL DEFAULT TRUE,
        "SensoryProfileJson" character varying(8000) NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_experience_guided_options" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_experience_guided_options_experience_guided_questions_QuestionId" FOREIGN KEY ("QuestionId") REFERENCES experience_guided_questions ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512000000_ExperienceCurationCms') THEN
    CREATE UNIQUE INDEX "IX_experience_guided_options_QuestionId_ExternalKey" ON experience_guided_options ("QuestionId", "ExternalKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512000000_ExperienceCurationCms') THEN
    CREATE TABLE experience_guided_affinities (
        "Id" uuid NOT NULL,
        "OptionId" uuid NOT NULL,
        "MenuItemId" uuid NOT NULL,
        "Weight" numeric(10,4) NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_experience_guided_affinities" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_experience_guided_affinities_experience_guided_options_OptionId" FOREIGN KEY ("OptionId") REFERENCES experience_guided_options ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_experience_guided_affinities_menu_items_MenuItemId" FOREIGN KEY ("MenuItemId") REFERENCES menu_items ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512000000_ExperienceCurationCms') THEN
    CREATE UNIQUE INDEX "IX_experience_guided_affinities_OptionId_MenuItemId" ON experience_guided_affinities ("OptionId", "MenuItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512000000_ExperienceCurationCms') THEN
    CREATE INDEX "IX_experience_guided_affinities_MenuItemId" ON experience_guided_affinities ("MenuItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512000000_ExperienceCurationCms') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260512000000_ExperienceCurationCms', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512184637_AddGuestExperienceAdminConfiguration') THEN
    ALTER TABLE experience_signature_slots ALTER COLUMN "SeasonalSpotlightEnabled" DROP DEFAULT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512184637_AddGuestExperienceAdminConfiguration') THEN
    ALTER TABLE experience_signature_slots ALTER COLUMN "IsSpotlight" DROP DEFAULT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512184637_AddGuestExperienceAdminConfiguration') THEN
    ALTER TABLE experience_guided_questions ALTER COLUMN "IsOptional" DROP DEFAULT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512184637_AddGuestExperienceAdminConfiguration') THEN
    ALTER TABLE experience_guided_questions ALTER COLUMN "IsEnabled" DROP DEFAULT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512184637_AddGuestExperienceAdminConfiguration') THEN
    ALTER TABLE experience_guided_options ALTER COLUMN "IsEnabled" DROP DEFAULT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512184637_AddGuestExperienceAdminConfiguration') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260512184637_AddGuestExperienceAdminConfiguration', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512190554_GuestExperienceCmsEngine') THEN
    ALTER TABLE menu_items ADD "DiscoveryStory" character varying(2000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512190554_GuestExperienceCmsEngine') THEN
    ALTER TABLE menu_items ADD "IsDiscoveryEligible" boolean NOT NULL DEFAULT TRUE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512190554_GuestExperienceCmsEngine') THEN
    ALTER TABLE experience_signature_slots ADD "IsActive" boolean NOT NULL DEFAULT TRUE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512190554_GuestExperienceCmsEngine') THEN
    ALTER TABLE experience_guided_questions ADD "Description" character varying(2000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512190554_GuestExperienceCmsEngine') THEN
    ALTER TABLE experience_guided_options ADD "Description" character varying(1200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512190554_GuestExperienceCmsEngine') THEN
    ALTER TABLE experience_guided_options ADD "FlavorTagsJson" character varying(2000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512190554_GuestExperienceCmsEngine') THEN
    ALTER TABLE experience_guided_options ADD "MoodKey" character varying(120);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512190554_GuestExperienceCmsEngine') THEN
    ALTER TABLE experience_guided_options ADD "RefinementKey" character varying(120);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512190554_GuestExperienceCmsEngine') THEN
    ALTER TABLE experience_guided_options ADD "WeightMultiplier" numeric(10,4) NOT NULL DEFAULT 1.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512190554_GuestExperienceCmsEngine') THEN
    ALTER TABLE experience_discovery_settings ADD "AdventureTone" integer NOT NULL DEFAULT 3;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512190554_GuestExperienceCmsEngine') THEN
    ALTER TABLE experience_discovery_settings ADD "AllowRerolls" boolean NOT NULL DEFAULT TRUE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512190554_GuestExperienceCmsEngine') THEN
    ALTER TABLE experience_discovery_settings ADD "AllowSeasonalCups" boolean NOT NULL DEFAULT TRUE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512190554_GuestExperienceCmsEngine') THEN
    ALTER TABLE experience_discovery_settings ADD "PreferSignaturesFirst" boolean NOT NULL DEFAULT TRUE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512190554_GuestExperienceCmsEngine') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260512190554_GuestExperienceCmsEngine', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260513031649_ExperienceWorkbenchSnapshots') THEN
    CREATE TABLE experience_snapshots (
        "Id" uuid NOT NULL,
        "Kind" smallint NOT NULL DEFAULT 0,
        "PayloadJson" text NOT NULL,
        "HouseNote" character varying(400),
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_experience_snapshots" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260513031649_ExperienceWorkbenchSnapshots') THEN
    CREATE TABLE experience_publish_records (
        "Id" uuid NOT NULL,
        "SnapshotId" uuid NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_experience_publish_records" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_experience_publish_records_experience_snapshots_SnapshotId" FOREIGN KEY ("SnapshotId") REFERENCES experience_snapshots ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260513031649_ExperienceWorkbenchSnapshots') THEN
    CREATE INDEX "IX_experience_publish_records_CreatedAtUtc" ON experience_publish_records ("CreatedAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260513031649_ExperienceWorkbenchSnapshots') THEN
    CREATE INDEX "IX_experience_publish_records_SnapshotId" ON experience_publish_records ("SnapshotId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260513031649_ExperienceWorkbenchSnapshots') THEN
    CREATE INDEX "IX_experience_snapshots_Kind_CreatedAtUtc" ON experience_snapshots ("Kind", "CreatedAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260513031649_ExperienceWorkbenchSnapshots') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260513031649_ExperienceWorkbenchSnapshots', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260513034800_MenuCatalogAndGroupExperience') THEN
    ALTER TABLE menu_items ADD "CatalogKey" character varying(160);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260513034800_MenuCatalogAndGroupExperience') THEN
    ALTER TABLE menu_items ADD "IconGlyph" character varying(32);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260513034800_MenuCatalogAndGroupExperience') THEN
    ALTER TABLE menu_items ADD "ItemType" character varying(80);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260513034800_MenuCatalogAndGroupExperience') THEN
    CREATE TABLE experience_group_settings (
        "Id" uuid NOT NULL,
        "ArrivalKicker" character varying(240) NOT NULL,
        "GuestCountPrompt" character varying(400) NOT NULL,
        "GuestCountLead" character varying(800),
        "MinGuests" integer NOT NULL DEFAULT 1,
        "MaxGuests" integer NOT NULL DEFAULT 8,
        "GuestTabsIntro" character varying(800),
        "GuestDoneHint" character varying(800),
        "SummaryHeadline" character varying(400) NOT NULL,
        "SummaryLead" character varying(1200),
        "HospitalityClosing" character varying(1200),
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_experience_group_settings" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260513034800_MenuCatalogAndGroupExperience') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260513034800_MenuCatalogAndGroupExperience', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260513042537_AddMenuItemDisplaySortOrder') THEN
    ALTER TABLE menu_items ADD "DisplaySortOrder" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260513042537_AddMenuItemDisplaySortOrder') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260513042537_AddMenuItemDisplaySortOrder', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260513071229_AddAppNetworkSettings') THEN
    CREATE TABLE app_network_settings (
        "Id" uuid NOT NULL,
        "PublicBaseUrlOverride" character varying(2000),
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_app_network_settings" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260513071229_AddAppNetworkSettings') THEN
    INSERT INTO app_network_settings ("Id", "PublicBaseUrlOverride", "CreatedAtUtc", "UpdatedAtUtc")
    VALUES ('cccccccc-cccc-cccc-cccc-ccccccccccc1', NULL, TIMESTAMPTZ '2026-05-13T00:00:00+00:00', NULL);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260513071229_AddAppNetworkSettings') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260513071229_AddAppNetworkSettings', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260514120000_AddLetterRoomContentJson') THEN
    ALTER TABLE experience_discovery_settings ADD "LetterRoomContentJson" character varying(16000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260514120000_AddLetterRoomContentJson') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260514120000_AddLetterRoomContentJson', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260516050910_AddKiotVietIntegrationPhase1A') THEN
    ALTER TABLE venue_tables ADD "KiotVietBranchId" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260516050910_AddKiotVietIntegrationPhase1A') THEN
    ALTER TABLE venue_tables ADD "KiotVietTableId" character varying(64);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260516050910_AddKiotVietIntegrationPhase1A') THEN
    CREATE TABLE kiotviet_outbox_messages (
        "Id" uuid NOT NULL,
        "OrderId" uuid NOT NULL,
        "EventType" character varying(64) NOT NULL,
        "Payload" text NOT NULL,
        "Status" integer NOT NULL,
        "RetryCount" integer NOT NULL,
        "NextRetryAtUtc" timestamp with time zone,
        "ProcessedAtUtc" timestamp with time zone,
        "KiotVietOrderId" character varying(64),
        "FailureReason" character varying(4000),
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_kiotviet_outbox_messages" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_kiotviet_outbox_messages_orders_OrderId" FOREIGN KEY ("OrderId") REFERENCES orders ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260516050910_AddKiotVietIntegrationPhase1A') THEN
    CREATE TABLE kiotviet_product_mappings (
        "MenuItemId" uuid NOT NULL,
        "KiotVietProductCode" character varying(64) NOT NULL,
        "KiotVietProductName" character varying(512),
        "IsActive" boolean NOT NULL,
        "LastSyncedAtUtc" timestamp with time zone NOT NULL,
        "SyncNote" character varying(1000),
        CONSTRAINT "PK_kiotviet_product_mappings" PRIMARY KEY ("MenuItemId"),
        CONSTRAINT "FK_kiotviet_product_mappings_menu_items_MenuItemId" FOREIGN KEY ("MenuItemId") REFERENCES menu_items ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260516050910_AddKiotVietIntegrationPhase1A') THEN
    CREATE TABLE kiotviet_sync_logs (
        "Id" bigint GENERATED ALWAYS AS IDENTITY,
        "SyncKind" character varying(64) NOT NULL,
        "IsSuccess" boolean NOT NULL,
        "ReferenceId" character varying(80),
        "KiotVietReference" character varying(80),
        "HttpStatusCode" integer,
        "FailureReason" character varying(4000),
        "DurationMs" bigint NOT NULL,
        "OccurredAtUtc" timestamp with time zone NOT NULL,
        "Detail" character varying(4000),
        CONSTRAINT "PK_kiotviet_sync_logs" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260516050910_AddKiotVietIntegrationPhase1A') THEN
    CREATE INDEX ix_kv_outbox_order_id ON kiotviet_outbox_messages ("OrderId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260516050910_AddKiotVietIntegrationPhase1A') THEN
    CREATE INDEX ix_kv_outbox_status_retry ON kiotviet_outbox_messages ("Status", "NextRetryAtUtc") WHERE "Status" IN (0, 3);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260516050910_AddKiotVietIntegrationPhase1A') THEN
    CREATE UNIQUE INDEX ix_kv_product_code ON kiotviet_product_mappings ("KiotVietProductCode");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260516050910_AddKiotVietIntegrationPhase1A') THEN
    CREATE INDEX ix_kv_sync_logs_kind_success_time ON kiotviet_sync_logs ("SyncKind", "IsSuccess", "OccurredAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260516050910_AddKiotVietIntegrationPhase1A') THEN
    CREATE INDEX ix_kv_sync_logs_time ON kiotviet_sync_logs ("OccurredAtUtc" DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260516050910_AddKiotVietIntegrationPhase1A') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260516050910_AddKiotVietIntegrationPhase1A', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260517150641_AddSommelierSetKey') THEN
    DROP INDEX "IX_experience_guided_questions_ExternalKey";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260517150641_AddSommelierSetKey') THEN
    ALTER TABLE experience_guided_questions ADD "SetKey" character varying(100) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260517150641_AddSommelierSetKey') THEN
    UPDATE "experience_guided_questions" SET "SetKey" = 'atelier_v1' WHERE "SetKey" = '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260517150641_AddSommelierSetKey') THEN
    CREATE UNIQUE INDEX "IX_experience_guided_questions_SetKey_ExternalKey" ON experience_guided_questions ("SetKey", "ExternalKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260517150641_AddSommelierSetKey') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260517150641_AddSommelierSetKey', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260520085807_AddDetailPosterImagePath') THEN
    ALTER TABLE menu_items ADD "DetailPosterImagePath" character varying(2000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260520085807_AddDetailPosterImagePath') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260520085807_AddDetailPosterImagePath', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260520191504_AddDrinkProvenanceFields') THEN
    ALTER TABLE menu_items ADD "Certification" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260520191504_AddDrinkProvenanceFields') THEN
    ALTER TABLE menu_items ADD "Origin" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260520191504_AddDrinkProvenanceFields') THEN
    ALTER TABLE menu_items ADD "ProducerStory" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260520191504_AddDrinkProvenanceFields') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260520191504_AddDrinkProvenanceFields', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260526082411_AddHomepageExperienceSettings') THEN
    CREATE TABLE homepage_experience_settings (
        "Id" uuid NOT NULL,
        "IsGroupEnabled" boolean NOT NULL DEFAULT TRUE,
        "IsSoloEnabled" boolean NOT NULL DEFAULT TRUE,
        "IsSommelierEnabled" boolean NOT NULL DEFAULT TRUE,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_homepage_experience_settings" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260526082411_AddHomepageExperienceSettings') THEN
    INSERT INTO homepage_experience_settings ("Id", "IsGroupEnabled", "IsSoloEnabled", "IsSommelierEnabled", "CreatedAtUtc", "UpdatedAtUtc")
    VALUES ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb8', TRUE, TRUE, TRUE, TIMESTAMPTZ '2026-05-26T00:00:00+00:00', NULL);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260526082411_AddHomepageExperienceSettings') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260526082411_AddHomepageExperienceSettings', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260605142000_Phase2_OrderItemSnapshotAndSafety') THEN
    ALTER TABLE order_items ADD "MenuItemName" character varying(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260605142000_Phase2_OrderItemSnapshotAndSafety') THEN

    UPDATE order_items oi
    SET "MenuItemName" = mi."Name"
    FROM menu_items mi
    WHERE oi."MenuItemId" = mi."Id"
      AND oi."MenuItemName" IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260605142000_Phase2_OrderItemSnapshotAndSafety') THEN
    ALTER TABLE order_items DROP CONSTRAINT "FK_order_items_menu_items_MenuItemId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260605142000_Phase2_OrderItemSnapshotAndSafety') THEN
    ALTER TABLE order_items ADD CONSTRAINT "FK_order_items_menu_items_MenuItemId" FOREIGN KEY ("MenuItemId") REFERENCES menu_items ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260605142000_Phase2_OrderItemSnapshotAndSafety') THEN

    ALTER TABLE orders
    ADD CONSTRAINT "CK_orders_Status_Valid"
    CHECK ("Status" IN (0, 1, 2, 3, 4, 5, 6));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260605142000_Phase2_OrderItemSnapshotAndSafety') THEN

    ALTER TABLE order_items
    ADD CONSTRAINT "CK_order_items_Quantity_Positive"
    CHECK ("Quantity" > 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260605142000_Phase2_OrderItemSnapshotAndSafety') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260605142000_Phase2_OrderItemSnapshotAndSafety', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260605160000_Phase3_StaffBoardStatusIndex') THEN
    CREATE INDEX "IX_orders_Status_CreatedAtUtc" ON orders ("Status", "CreatedAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260605160000_Phase3_StaffBoardStatusIndex') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260605160000_Phase3_StaffBoardStatusIndex', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260625120000_AddOrderPaymentWorkflow') THEN
    ALTER TABLE orders DROP CONSTRAINT "CK_orders_Status_Valid";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260625120000_AddOrderPaymentWorkflow') THEN
    ALTER TABLE orders ADD "BillNumber" character varying(24);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260625120000_AddOrderPaymentWorkflow') THEN
    ALTER TABLE orders ADD "CompletedAtUtc" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260625120000_AddOrderPaymentWorkflow') THEN
    ALTER TABLE orders ADD "PaidAtUtc" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260625120000_AddOrderPaymentWorkflow') THEN
    ALTER TABLE orders ADD "PaymentConfirmedBy" character varying(120);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260625120000_AddOrderPaymentWorkflow') THEN
    ALTER TABLE orders ADD "PaymentMethod" character varying(40);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260625120000_AddOrderPaymentWorkflow') THEN
    ALTER TABLE orders ADD CONSTRAINT "CK_orders_Status_Valid" CHECK ("Status" IN (0, 1, 2, 3, 4, 5, 6, 7));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260625120000_AddOrderPaymentWorkflow') THEN
    UPDATE orders
    SET "CompletedAtUtc" = COALESCE("UpdatedAtUtc", "CreatedAtUtc")
    WHERE "Status" = 4 AND "CompletedAtUtc" IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260625120000_AddOrderPaymentWorkflow') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260625120000_AddOrderPaymentWorkflow', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626120000_AddOrderItemPreparation') THEN
    ALTER TABLE order_items ADD "PreparedAtUtc" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626120000_AddOrderItemPreparation') THEN
    ALTER TABLE order_items ADD "PreparedBy" character varying(120);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626120000_AddOrderItemPreparation') THEN
    ALTER TABLE order_items ADD "PreparedQuantity" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626120000_AddOrderItemPreparation') THEN
    ALTER TABLE order_items ADD CONSTRAINT "CK_order_items_PreparedQuantity_Range" CHECK ("PreparedQuantity" >= 0 AND "PreparedQuantity" <= "Quantity");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626120000_AddOrderItemPreparation') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260626120000_AddOrderItemPreparation', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260702120000_AddPaymentConfirmations') THEN
    CREATE TABLE payment_confirmations (
        "Id" uuid NOT NULL,
        "Provider" character varying(64) NOT NULL,
        "ProviderTransactionId" character varying(128),
        "ReceivedAtUtc" timestamp with time zone NOT NULL,
        "Amount" numeric(12,2) NOT NULL,
        "Memo" character varying(500) NOT NULL,
        "AccountNumber" character varying(64),
        "BankCode" character varying(32),
        "RawPayloadJson" character varying(8000),
        "MatchedOrderId" uuid,
        "MatchStatus" character varying(32) NOT NULL,
        "ProcessedAtUtc" timestamp with time zone,
        "Notes" character varying(500),
        CONSTRAINT "PK_payment_confirmations" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260702120000_AddPaymentConfirmations') THEN
    CREATE INDEX "IX_payment_confirmations_MatchedOrderId" ON payment_confirmations ("MatchedOrderId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260702120000_AddPaymentConfirmations') THEN
    CREATE INDEX "IX_payment_confirmations_Memo" ON payment_confirmations ("Memo");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260702120000_AddPaymentConfirmations') THEN
    CREATE UNIQUE INDEX "IX_payment_confirmations_Provider_ProviderTransactionId" ON payment_confirmations ("Provider", "ProviderTransactionId") WHERE "ProviderTransactionId" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260702120000_AddPaymentConfirmations') THEN
    CREATE INDEX "IX_payment_confirmations_ReceivedAtUtc" ON payment_confirmations ("ReceivedAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260702120000_AddPaymentConfirmations') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260702120000_AddPaymentConfirmations', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260704120000_AddStaffAccounts') THEN
    ALTER TABLE orders ADD "PaymentConfirmedByAccountId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260704120000_AddStaffAccounts') THEN
    CREATE TABLE staff_accounts (
        "Id" uuid NOT NULL,
        "Username" character varying(64) NOT NULL,
        "DisplayName" character varying(120) NOT NULL,
        "PasswordHash" character varying(512) NOT NULL,
        "Role" character varying(32) NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        "LastLoginAtUtc" timestamp with time zone,
        "CreatedBy" character varying(120),
        CONSTRAINT "PK_staff_accounts" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260704120000_AddStaffAccounts') THEN
    CREATE INDEX "IX_orders_PaymentConfirmedByAccountId" ON orders ("PaymentConfirmedByAccountId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260704120000_AddStaffAccounts') THEN
    CREATE INDEX "IX_staff_accounts_IsActive" ON staff_accounts ("IsActive");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260704120000_AddStaffAccounts') THEN
    CREATE UNIQUE INDEX "IX_staff_accounts_Username" ON staff_accounts ("Username");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260704120000_AddStaffAccounts') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260704120000_AddStaffAccounts', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260705120000_AddShiftCloses') THEN
    CREATE TABLE shift_closes (
        "Id" uuid NOT NULL,
        "OpenedAtUtc" timestamp with time zone NOT NULL,
        "ClosedAtUtc" timestamp with time zone NOT NULL,
        "ClosedBy" character varying(120) NOT NULL,
        "ClosedByAccountId" uuid,
        "TotalOrders" integer NOT NULL,
        "TotalGrossAmount" numeric(18,2) NOT NULL,
        "CashOrCardAmount" numeric(18,2) NOT NULL,
        "BankTransferAmount" numeric(18,2) NOT NULL,
        "UnknownPaymentAmount" numeric(18,2) NOT NULL,
        "CashOrCardOrders" integer NOT NULL,
        "BankTransferOrders" integer NOT NULL,
        "UnknownPaymentOrders" integer NOT NULL,
        "SnapshotJson" jsonb NOT NULL,
        "Notes" character varying(500),
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_shift_closes" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260705120000_AddShiftCloses') THEN
    CREATE INDEX "IX_shift_closes_ClosedAtUtc" ON shift_closes ("ClosedAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260705120000_AddShiftCloses') THEN
    CREATE INDEX "IX_shift_closes_ClosedByAccountId" ON shift_closes ("ClosedByAccountId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260705120000_AddShiftCloses') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260705120000_AddShiftCloses', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260705140000_AddBaristaAttribution') THEN
    ALTER TABLE orders ADD "CompletedBy" character varying(120);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260705140000_AddBaristaAttribution') THEN
    ALTER TABLE orders ADD "CompletedByAccountId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260705140000_AddBaristaAttribution') THEN
    ALTER TABLE order_items ADD "PreparedByAccountId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260705140000_AddBaristaAttribution') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260705140000_AddBaristaAttribution', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260706140000_AddOrderCustomerNote') THEN
    ALTER TABLE orders ADD "CustomerNote" character varying(300);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260706140000_AddOrderCustomerNote') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260706140000_AddOrderCustomerNote', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260707120000_AddOrderItemCustomerNote') THEN
    ALTER TABLE order_items ADD "CustomerNote" character varying(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260707120000_AddOrderItemCustomerNote') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260707120000_AddOrderItemCustomerNote', '8.0.8');
    END IF;
END $EF$;
COMMIT;

