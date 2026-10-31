-- Manual PostgreSQL acceptance check. All data changes roll back.
\set ON_ERROR_STOP on
BEGIN;
CREATE FUNCTION pg_temp.expect_rejection(command text, expected_state text) RETURNS void
LANGUAGE plpgsql AS $$
BEGIN
    EXECUTE command;
    RAISE EXCEPTION 'Expected SQLSTATE % but statement succeeded', expected_state;
EXCEPTION WHEN OTHERS THEN
    IF SQLSTATE <> expected_state THEN RAISE; END IF;
END;
$$;

DO $$
DECLARE
    alice text := gen_random_uuid()::text;
    bob text := gen_random_uuid()::text;
    external_id text := gen_random_uuid()::text;
    event_id bigint;
    delivery_id bigint;
BEGIN
    IF (SELECT array_agg("Code"::text ORDER BY "Id") FROM "Topics") <> ARRAY['finance','politics','weather']
        OR (SELECT count(*) FROM "Topics") <> 3
        OR (SELECT array_agg("Code"::text ORDER BY "Id") FROM "Channels") <> ARRAY['email','slack']
        OR (SELECT count(*) FROM "Channels") <> 2 THEN
        RAISE EXCEPTION 'Unexpected catalog seeds (use a fresh validation database)';
    END IF;
    INSERT INTO "AspNetUsers" ("Id", "IsEnabled", "EmailConfirmed", "PhoneNumberConfirmed",
        "TwoFactorEnabled", "LockoutEnabled", "AccessFailedCount")
        VALUES (alice, true, false, false, false, false, 0), (bob, true, false, false, false, false, 0);
    INSERT INTO "UserTopicSelections" VALUES (alice, 1), (alice, 3);
    INSERT INTO "UserChannelSelections" VALUES (alice, 1), (alice, 2);
    DELETE FROM "UserChannelSelections" WHERE "UserId" = alice AND "ChannelId" = 2;
    IF (SELECT count(*) FROM "UserTopicSelections" WHERE "UserId" = alice) <> 2 THEN
        RAISE EXCEPTION 'Removing a channel changed topics';
    END IF;
    DELETE FROM "UserTopicSelections" WHERE "UserId" = alice AND "TopicId" = 3;
    IF (SELECT count(*) FROM "UserChannelSelections" WHERE "UserId" = alice) <> 1 THEN
        RAISE EXCEPTION 'Removing a topic changed channels';
    END IF;
    PERFORM pg_temp.expect_rejection(format('INSERT INTO "UserTopicSelections" VALUES (%L, 1)', alice), '23505');
    PERFORM pg_temp.expect_rejection(format('INSERT INTO "UserChannelSelections" VALUES (%L, 1)', alice), '23505');
    PERFORM pg_temp.expect_rejection(format('INSERT INTO "UserTopicSelections" VALUES (%L, 999999)', alice), '23503');
    PERFORM pg_temp.expect_rejection(format('INSERT INTO "UserChannelSelections" VALUES (%L, 999999)', alice), '23503');
    PERFORM pg_temp.expect_rejection('INSERT INTO "UserTopicSelections" VALUES (''missing-user'', 1)', '23503');
    PERFORM pg_temp.expect_rejection('INSERT INTO "Topics" ("Code", "DisplayName", "IsEnabled") VALUES (''finance'', ''Duplicate'', true)', '23505');
    PERFORM pg_temp.expect_rejection('INSERT INTO "Channels" ("Code", "DisplayName", "IsEnabled") VALUES (''email'', ''Duplicate'', true)', '23505');
    INSERT INTO "AlertEvents" ("ExternalEventId", "TopicId", "Title", "Body", "OccurredAt", "ReceivedAt")
        VALUES (external_id, 1, 'Validation', 'Sample', now(), now()) RETURNING "Id" INTO event_id;
    PERFORM pg_temp.expect_rejection(format('INSERT INTO "AlertEvents" ("ExternalEventId", "TopicId", "Title", "Body", "OccurredAt", "ReceivedAt") VALUES (%L, 1, ''Duplicate'', '''', now(), now())', external_id), '23505');
    INSERT INTO "Deliveries" ("EventId", "UserId", "ChannelId", "Status", "AttemptCount", "CreatedAt")
        VALUES (event_id, alice, 1, 'Pending', 0, now()) RETURNING "Id" INTO delivery_id;
    INSERT INTO "Deliveries" ("EventId", "UserId", "ChannelId", "Status", "AttemptCount", "CreatedAt")
        VALUES (event_id, bob, 1, 'Pending', 0, now());
    PERFORM pg_temp.expect_rejection(format('INSERT INTO "Deliveries" ("EventId", "UserId", "ChannelId", "Status", "AttemptCount", "CreatedAt") VALUES (%s, %L, 1, ''Pending'', 0, now())', event_id, alice), '23505');
    PERFORM pg_temp.expect_rejection(format('UPDATE "Deliveries" SET "AttemptCount" = 4 WHERE "Id" = %s', delivery_id), '23514');
    PERFORM pg_temp.expect_rejection(format('UPDATE "Deliveries" SET "AttemptCount" = -1 WHERE "Id" = %s', delivery_id), '23514');
    PERFORM pg_temp.expect_rejection(format('UPDATE "Deliveries" SET "Status" = ''Unknown'' WHERE "Id" = %s', delivery_id), '23514');
    UPDATE "Deliveries" SET "AttemptCount" = 3, "Status" = 'Failed', "LastError" = 'safe-error-code',
        "LastAttemptAt" = now(), "CompletedAt" = now() WHERE "Id" = delivery_id;
    PERFORM pg_temp.expect_rejection(format('UPDATE "Deliveries" SET "UserId" = ''missing-user'' WHERE "Id" = %s', delivery_id), '23503');
    PERFORM pg_temp.expect_rejection(format('UPDATE "Deliveries" SET "ChannelId" = 999999 WHERE "Id" = %s', delivery_id), '23503');
    PERFORM pg_temp.expect_rejection(format('UPDATE "Deliveries" SET "EventId" = -1 WHERE "Id" = %s', delivery_id), '23503');
    PERFORM pg_temp.expect_rejection(format('UPDATE "AlertEvents" SET "TopicId" = 999999 WHERE "Id" = %s', event_id), '23503');
    PERFORM pg_temp.expect_rejection('DELETE FROM "Topics" WHERE "Id" = 1', '23503');
    PERFORM pg_temp.expect_rejection('DELETE FROM "Channels" WHERE "Id" = 1', '23503');
    PERFORM pg_temp.expect_rejection(format('DELETE FROM "AlertEvents" WHERE "Id" = %s', event_id), '23503');
    DELETE FROM "AspNetUsers" WHERE "Id" = alice;
    IF EXISTS (SELECT 1 FROM "UserTopicSelections" WHERE "UserId" = alice)
        OR EXISTS (SELECT 1 FROM "UserChannelSelections" WHERE "UserId" = alice)
        OR EXISTS (SELECT 1 FROM "Deliveries" WHERE "UserId" = alice)
        OR NOT EXISTS (SELECT 1 FROM "AlertEvents" WHERE "Id" = event_id)
        OR NOT EXISTS (SELECT 1 FROM "Deliveries" WHERE "UserId" = bob)
        OR NOT EXISTS (SELECT 1 FROM "Topics" WHERE "Id" = 1)
        OR NOT EXISTS (SELECT 1 FROM "Channels" WHERE "Id" = 1) THEN
        RAISE EXCEPTION 'User cascade/history policy violated';
    END IF;
    RAISE NOTICE 'PASS: seeds, independent selections, uniqueness, FKs, attempt/status limits, deletion/history';
END;
$$;
ROLLBACK;
