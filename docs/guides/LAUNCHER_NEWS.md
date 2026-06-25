# Launcher News Guide

Echo Gate shows launcher news on the Home tab. The launcher reads news from the configured launcher service URL, usually:

```text
http://127.0.0.1:8080/launcher/news
```

The backend source is the `launcher_news` table in the local AetherXIV database.

![Echo Gate home tab](../../Home.png)

## Table Fields

`Data/sql/launcher_services.sql` creates this table during database setup:

| Field | Purpose |
| --- | --- |
| `id` | Auto-generated row id. |
| `title` | Main headline shown by the launcher. |
| `summary` | Short summary shown in the launcher card. |
| `body` | Optional longer body text for future/full views. |
| `banner_url` | Optional image URL. Current launcher builds may not show this everywhere yet. |
| `link_url` | Optional external link for future/full views. |
| `published_at` | Publish time. The endpoint returns it in ISO format. |
| `is_published` | `1` shows the post, `0` hides it. |
| `sort_order` | Lower numbers appear first. Use this to pin important posts. |

The endpoint returns only published rows:

```sql
WHERE is_published = 1
ORDER BY sort_order ASC, published_at DESC
LIMIT 10
```

## Add A News Post

Open MariaDB with the local app account:

```sh
mariadb -u meteor -pmeteor_dev ffxiv_server
```

Add a normal post:

```sql
INSERT INTO launcher_news
  (title, summary, body, published_at, is_published, sort_order)
VALUES
  (
    'Server maintenance complete',
    'The local test server is back online with the latest fixes.',
    'Players should restart Echo Gate and log in again before testing.',
    UTC_TIMESTAMP(),
    1,
    10
  );
```

Use a lower `sort_order` to pin something above regular posts:

```sql
INSERT INTO launcher_news
  (title, summary, body, published_at, is_published, sort_order)
VALUES
  (
    'Read before testing',
    'Known login and map issues are listed in Discord.',
    'Please report launch crashes with Echo Gate logs attached.',
    UTC_TIMESTAMP(),
    1,
    0
  );
```

## Edit Or Hide Posts

Edit a typo:

```sql
UPDATE launcher_news
SET summary = 'The local test server is online with the latest fixes.'
WHERE id = 2;
```

Hide a post without deleting it:

```sql
UPDATE launcher_news
SET is_published = 0
WHERE id = 2;
```

Publish a draft:

```sql
UPDATE launcher_news
SET is_published = 1,
    published_at = UTC_TIMESTAMP()
WHERE id = 2;
```

Move a post lower in the list:

```sql
UPDATE launcher_news
SET sort_order = 50
WHERE id = 2;
```

## Check What The Launcher Will See

List the visible posts in the same order Echo Gate receives them:

```sql
SELECT id, title, summary, published_at, sort_order
FROM launcher_news
WHERE is_published = 1
ORDER BY sort_order ASC, published_at DESC
LIMIT 10;
```

Check the HTTP endpoint:

```sh
curl http://127.0.0.1:8080/launcher/news
```

If Echo Gate is already open, use the Server tab's refresh action or restart the launcher to fetch the latest service data.

## Suggested Posting Style

- Keep `title` short enough to scan in the launcher.
- Keep `summary` to one or two sentences.
- Put longer details in `body`.
- Use `sort_order` `0` for the most important pinned post.
- Use `sort_order` `10`, `20`, `30`, and so on for normal posts so there is room to insert new posts later.
- Hide old posts with `is_published = 0` instead of deleting them.

## Current Limitations

- There is no browser admin panel for launcher news yet.
- `banner_url` and `link_url` are part of the service contract, but current launcher builds may not expose every field in the Home tab UI.
- News is server-specific. If you run a remote public server and a local test server, each database has its own `launcher_news` rows.
