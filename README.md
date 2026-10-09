# Acadex

Acadex exam platform backend.

## Web Push setup

Set these environment variables to enable browser push notifications:

- `VAPID_PUBLIC_KEY`
- `VAPID_PRIVATE_KEY`
- `VAPID_SUBJECT` (for example, `mailto:admin@example.com`)

Generate a key pair with:

```sh
npx web-push generate-vapid-keys
```

If any VAPID variable is missing or invalid, web push is disabled gracefully and `GET /api/push/public-key` returns `{ "publicKey": null }`.
