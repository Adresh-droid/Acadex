# Acadex drafts, classrooms, notifications, and push API contract

This document describes the additive backend routes. All authenticated routes use `Authorization: Bearer $TOKEN`. JSON request bodies use camelCase. Error responses use `{ "error": "..." }`.

Set these shell variables before trying the examples:

```sh
API=https://your-acadex-host.example
TEACHER_TOKEN=teacher_bearer_token
OTHER_TEACHER_TOKEN=another_teacher_bearer_token
STUDENT_TOKEN=student_bearer_token
OTHER_STUDENT_TOKEN=another_student_bearer_token
CLASSROOM_ID=classroom_id_owned_by_TEACHER_TOKEN
OTHER_CLASSROOM_ID=classroom_id_owned_by_OTHER_TEACHER_TOKEN
EXAM_ID=exam_id_owned_by_TEACHER_TOKEN
```

## Response contract

- Drafts: `GET /api/teacher/drafts` returns `{ drafts: [{ id, title, updatedAt, data }] }`; `PUT /api/teacher/drafts/:id` returns `{ id, updatedAt }`; delete returns `{ ok: true }`.
- Classrooms: teacher list returns `{ classrooms: [{ id, name, joinCode, memberCount, examCount, createdAt }] }`. Create returns one such object at the top level. Detail returns `{ classroom, members, exams }`. Members expose `userId, displayName, className, email, joinedAt`; exams expose `examId, title, type, assignedAt, submittedCount, memberCount`.
- Membership: add members returns `{ added, skipped }`; join returns `{ classroom: { id, name, teacherName } }`; leave/remove returns `{ ok: true }`.
- Exam links: `POST /exam/create` optionally accepts `classroomIds` (at most 20, all owned by the teacher). `PATCH /api/teacher/exams/:id` optionally accepts `classroomIds` as the full replacement set; omitted means unchanged. Teacher exam GET adds `classroomIds`.
- Student assignments: `GET /api/student/assignments` returns `{ assignments: [{ assignmentId, examId, title, type, durationMs, classroomId, classroomName, teacherName, assignedAt, url, done, bestPercentage }] }`. Each exam appears once; the earliest classroom link is selected.
- Notifications: GET returns `{ notifications: [{ id, type, title, body, examId, classroomId, createdAt, readAt }], unreadCount }`. POST read accepts `{ ids: [...] }` or `{ all: true }`. Each account can read/update only its own notification rows.
- Push: GET public-key returns `{ publicKey }` or `{ publicKey: null }` when VAPID is not configured. Subscribe/unsubscribe return `{ ok: true }`.

## curl examples

### A. Drafts

List the caller's latest drafts:

```sh
curl "$API/api/teacher/drafts" -H "Authorization: Bearer $TEACHER_TOKEN"
```

Create/update an owned draft (the data value must be a plain JSON object, serialized size at most 512 KB):

```sh
curl -X PUT "$API/api/teacher/drafts/draft_1" \
  -H "Authorization: Bearer $TEACHER_TOKEN" -H "Content-Type: application/json" \
  -d '{"title":"Algebra practice","data":{"questions":[],"durationMs":1800000}}'
```

Delete an owned draft:

```sh
curl -X DELETE "$API/api/teacher/drafts/draft_1" -H "Authorization: Bearer $TEACHER_TOKEN"
```

### B. Teacher classrooms

List, create, rename/regenerate code, delete:

```sh
curl "$API/api/teacher/classrooms" -H "Authorization: Bearer $TEACHER_TOKEN"
curl -X POST "$API/api/teacher/classrooms" -H "Authorization: Bearer $TEACHER_TOKEN" -H "Content-Type: application/json" -d '{"name":"Class VIII A"}'
curl -X PATCH "$API/api/teacher/classrooms/$CLASSROOM_ID" -H "Authorization: Bearer $TEACHER_TOKEN" -H "Content-Type: application/json" -d '{"name":"Class VIII A - Morning","regenerateCode":true}'
curl -X DELETE "$API/api/teacher/classrooms/$CLASSROOM_ID" -H "Authorization: Bearer $TEACHER_TOKEN"
```

Get details, add eligible students, remove one member:

```sh
curl "$API/api/teacher/classrooms/$CLASSROOM_ID" -H "Authorization: Bearer $TEACHER_TOKEN"
curl -X POST "$API/api/teacher/classrooms/$CLASSROOM_ID/members" -H "Authorization: Bearer $TEACHER_TOKEN" -H "Content-Type: application/json" -d '{"studentUserIds":["student_user_id_1","student_user_id_2"]}'
curl -X DELETE "$API/api/teacher/classrooms/$CLASSROOM_ID/members/student_user_id_1" -H "Authorization: Bearer $TEACHER_TOKEN"
```

Only students who submitted at least one exam owned by that teacher can be added; ineligible IDs, existing members, and IDs beyond the 500-member cap are counted as skipped.

The existing teacher-student list now includes `userId` and `className` additively:

```sh
curl "$API/api/teacher/students" -H "Authorization: Bearer $TEACHER_TOKEN"
```

### C. Link classrooms to exams

Create a template exam and assign it to one or more classrooms. Keep the actual question schema consistent with the existing exam API:

```sh
curl -X POST "$API/exam/create" -H "Authorization: Bearer $TEACHER_TOKEN" -H "Content-Type: application/json" \
  -d '{"title":"Algebra quiz","studentPassword":"exam-password","durationMs":1800000,"type":"template","questions":[{"type":"mcq","text":"2 + 2 = ?","options":["3","4","5","6"],"answer":1}],"classroomIds":["'"$CLASSROOM_ID"'"]}'
```

Get the teacher-owned exam (response adds `classroomIds`):

```sh
curl "$API/api/teacher/exams/$EXAM_ID" -H "Authorization: Bearer $TEACHER_TOKEN"
```

Replace its classroom links; an empty array unlinks all classrooms, while omitting `classroomIds` leaves links unchanged:

```sh
curl -X PATCH "$API/api/teacher/exams/$EXAM_ID" -H "Authorization: Bearer $TEACHER_TOKEN" -H "Content-Type: application/json" \
  -d '{"classroomIds":["'"$CLASSROOM_ID"'"]}'
```

### D. Student classroom membership and assignments

Join using the 6-character code, list classrooms, leave, and list assigned exams:

```sh
curl -X POST "$API/api/student/classrooms/join" -H "Authorization: Bearer $STUDENT_TOKEN" -H "Content-Type: application/json" -d '{"code":"ABC234"}'
curl "$API/api/student/classrooms" -H "Authorization: Bearer $STUDENT_TOKEN"
curl -X DELETE "$API/api/student/classrooms/$CLASSROOM_ID" -H "Authorization: Bearer $STUDENT_TOKEN"
curl "$API/api/student/assignments" -H "Authorization: Bearer $STUDENT_TOKEN"
```

The example join code is illustrative; use a real code returned by the teacher classroom API.

### E. In-app notifications

Read your own notifications and mark selected notifications or all notifications as read:

```sh
curl "$API/api/notifications?limit=30" -H "Authorization: Bearer $STUDENT_TOKEN"
curl -X POST "$API/api/notifications/read" -H "Authorization: Bearer $STUDENT_TOKEN" -H "Content-Type: application/json" -d '{"ids":["notification_id_1","notification_id_2"]}'
curl -X POST "$API/api/notifications/read" -H "Authorization: Bearer $STUDENT_TOKEN" -H "Content-Type: application/json" -d '{"all":true}'
```

### F. Web push

Get the public VAPID key:

```sh
curl "$API/api/push/public-key" -H "Authorization: Bearer $STUDENT_TOKEN"
```

Subscribe using a real `PushSubscription` object returned by the browser Push API:

```sh
curl -X POST "$API/api/push/subscribe" -H "Authorization: Bearer $STUDENT_TOKEN" -H "Content-Type: application/json" \
  -d '{"subscription":{"endpoint":"https://push-service.example/subscription/replace-with-real-endpoint","keys":{"p256dh":"replace-with-real-p256dh","auth":"replace-with-real-auth"}}}'
```

Unsubscribe the caller's endpoint:

```sh
curl -X POST "$API/api/push/unsubscribe" -H "Authorization: Bearer $STUDENT_TOKEN" -H "Content-Type: application/json" \
  -d '{"endpoint":"https://push-service.example/subscription/replace-with-real-endpoint"}'
```

### G. Cross-user authorization checks

A teacher must not read another teacher's classroom. This should return `404 { "error": "Classroom not found." }`:

```sh
curl -i "$API/api/teacher/classrooms/$OTHER_CLASSROOM_ID" -H "Authorization: Bearer $TEACHER_TOKEN"
```

A teacher cannot link an exam to a classroom owned by another teacher. This should return `400 { "error": "Invalid classroom." }` and must not create a link:

```sh
curl -i -X PATCH "$API/api/teacher/exams/$EXAM_ID" -H "Authorization: Bearer $TEACHER_TOKEN" -H "Content-Type: application/json" \
  -d '{"classroomIds":["'"$OTHER_CLASSROOM_ID"'"]}'
```

A student can only list their own notifications. The API has no user-id parameter to select another account's rows. Trying to mark another student's notification ID as read with this student's token must not update the other row:

```sh
curl "$API/api/notifications" -H "Authorization: Bearer $STUDENT_TOKEN"
curl -X POST "$API/api/notifications/read" -H "Authorization: Bearer $STUDENT_TOKEN" -H "Content-Type: application/json" -d '{"ids":["notification_id_belonging_to_other_student"]}'
```

Unsubscribing an endpoint owned by another account returns `{ "ok": true }` for idempotence, but deletes no row because the delete is scoped to the caller's user ID:

```sh
curl -X POST "$API/api/push/unsubscribe" -H "Authorization: Bearer $STUDENT_TOKEN" -H "Content-Type: application/json" \
  -d '{"endpoint":"https://push-service.example/endpoint-owned-by-other-account"}'
```

A student also cannot use teacher-only classroom routes, and a teacher cannot use student-only membership routes; role checks return 403.

## Idempotent migration SQL

These are the new table/index statements added to the existing `initDatabase()` block. IDs use TEXT to match `users.id` and `exams.id`.

```sql
CREATE TABLE IF NOT EXISTS exam_drafts (
  id TEXT PRIMARY KEY,
  owner_user_id TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  title TEXT NOT NULL DEFAULT '',
  data_json JSONB NOT NULL,
  updated_at BIGINT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_exam_drafts_owner_updated ON exam_drafts(owner_user_id, updated_at DESC);

CREATE TABLE IF NOT EXISTS classrooms (
  id TEXT PRIMARY KEY,
  owner_user_id TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  name TEXT NOT NULL,
  join_code TEXT NOT NULL UNIQUE,
  created_at BIGINT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_classrooms_owner ON classrooms(owner_user_id);

CREATE TABLE IF NOT EXISTS classroom_members (
  classroom_id TEXT NOT NULL REFERENCES classrooms(id) ON DELETE CASCADE,
  student_user_id TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  joined_at BIGINT NOT NULL,
  PRIMARY KEY(classroom_id, student_user_id)
);
CREATE INDEX IF NOT EXISTS idx_classroom_members_student ON classroom_members(student_user_id);

CREATE TABLE IF NOT EXISTS exam_classrooms (
  exam_id TEXT NOT NULL REFERENCES exams(id) ON DELETE CASCADE,
  classroom_id TEXT NOT NULL REFERENCES classrooms(id) ON DELETE CASCADE,
  assigned_at BIGINT NOT NULL,
  PRIMARY KEY(exam_id, classroom_id)
);
CREATE INDEX IF NOT EXISTS idx_exam_classrooms_class_assigned ON exam_classrooms(classroom_id, assigned_at DESC);
CREATE INDEX IF NOT EXISTS idx_exam_classrooms_exam ON exam_classrooms(exam_id);

CREATE TABLE IF NOT EXISTS notifications (
  id TEXT PRIMARY KEY,
  user_id TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  type TEXT NOT NULL,
  title TEXT NOT NULL,
  body TEXT NOT NULL DEFAULT '',
  exam_id TEXT NULL,
  classroom_id TEXT NULL,
  created_at BIGINT NOT NULL,
  read_at BIGINT NULL
);
CREATE INDEX IF NOT EXISTS idx_notifications_user_created ON notifications(user_id, created_at DESC);

CREATE TABLE IF NOT EXISTS push_subscriptions (
  endpoint TEXT PRIMARY KEY,
  user_id TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  p256dh TEXT NOT NULL,
  auth TEXT NOT NULL,
  created_at BIGINT NOT NULL,
  updated_at BIGINT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_push_subscriptions_user ON push_subscriptions(user_id);
```

## Risks and implementation notes

- `users.id`, `exams.id`, classroom IDs, notification IDs, and draft IDs are TEXT; the joins and array parameters intentionally use TEXT.
- Exam creation stores exam metadata, classroom links, and notifications in one DB transaction. B2 object upload happens before that transaction, so an unexpected DB failure after upload can leave an orphaned B2 object.
- Exam PATCH commits classroom link changes and notification inserts together. Exam content/metadata updates are performed before that link transaction, so a rare link transaction failure can leave the exam edit saved without the requested link replacement.
- A classroom can contain up to 500 students. Push sends are fire-and-forget in batches of 20; a large assignment can enqueue many deliveries, but they do not hold the API response open.
- Notification de-duplication is per student per exam, including students already notified by an earlier linked classroom. Unlinking a classroom does not retract existing notifications.
- Web push requires the browser frontend to request notification permission, register a service worker, create a PushSubscription using the returned VAPID public key, and POST that subscription. This backend patch does not add or alter files under `public/`.
- No live database/Render integration test was run here. JavaScript syntax was checked, but SQL behavior should be verified against a staging Neon database.
- The hosted `GET /exam/:id` page and files under `public/` were not edited.
