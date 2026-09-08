# Sync API Contract v1

Endpoint: `POST /api/v1/sync`

Request utama:
- `protocol_version = 1.0`
- `school_id`
- `device_id`
- `last_pull_revision`
- `push[]`
- `pull_limit`

Setiap rekod mempunyai `table`, `id`, `school_id`, `version`, `updated_at`, `deleted_at` dan `data`.
BLOB dikodkan sebagai `{ "__type": "base64", "data": "..." }`.

Response:
- `accepted[]`
- `conflicts[]`
- `changes[]`
- `next_pull_revision`
- `has_more`
- `server_time`

Konflik tidak menimpa rekod lokal pending; client menandakan `sync_status=conflict`.
