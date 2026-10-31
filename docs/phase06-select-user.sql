-- Development validation only: independently select two topics and Email.
-- Change the email to your existing enabled test user if needed.
INSERT INTO "UserTopicSelections" ("UserId", "TopicId")
SELECT u."Id", t."Id" FROM "AspNetUsers" u CROSS JOIN "Topics" t
WHERE u."Email" = 'user@example.test' AND t."Code" IN ('finance', 'weather')
ON CONFLICT DO NOTHING;

INSERT INTO "UserChannelSelections" ("UserId", "ChannelId")
SELECT u."Id", c."Id" FROM "AspNetUsers" u CROSS JOIN "Channels" c
WHERE u."Email" = 'user@example.test' AND c."Code" = 'email'
ON CONFLICT DO NOTHING;
